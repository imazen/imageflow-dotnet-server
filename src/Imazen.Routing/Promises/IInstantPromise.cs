using System.Buffers;
using System.Security.Cryptography;
using CommunityToolkit.HighPerformance.Buffers;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;
using Imazen.Routing.Helpers;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Promises;

public interface IInstantPromise
{
    bool IsCacheSupporting { get; }
    IRequestSnapshot FinalRequest { get; }

    // CodeResult would allow a lighter error path, but we also have http headers and maybe two ways to respond isn't best?
    ValueTask<IAdaptableHttpResponse> CreateResponseAsync(IRequestSnapshot request, IBlobRequestRouter router,
        IBlobPromisePipeline pipeline, CancellationToken cancellationToken = default);
}
public record PredefinedResponsePromise(IRequestSnapshot FinalRequest, IAdaptableReusableHttpResponse Response) : IInstantPromise{
    
    public bool IsCacheSupporting => false;
    
    public ValueTask<IAdaptableHttpResponse> CreateResponseAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline, CancellationToken cancellationToken = default)
    {
        return new ValueTask<IAdaptableHttpResponse>(Response);
    }
}


public interface IInstantCacheKeySupportingPromise: IInstantPromise
{
    // We only need to deal with dependencies if we are going to cache the final result.
    bool HasDependencies { get; }
    
    bool ReadyToWriteCacheKeyBasisData { get; }

    ValueTask<CodeResult> RouteDependenciesAsync(IBlobRequestRouter router, CancellationToken cancellationToken = default);
    
    // Must call resolve dependencies first
    void WriteCacheKeyBasisPairsToRecursive(IBufferWriter<byte> writer);

    byte[] GetCacheKey32Bytes();
    
    /// <summary>
    /// Should be implemented by source file providers and image processors.
    /// Cache promise layers should return null.
    /// </summary>
    LatencyTrackingZone? LatencyZone { get; }
}
public interface ICacheableBlobPromise : IInstantCacheKeySupportingPromise
{
    bool SupportsPreSignedUrls { get; }
    
    ValueTask<CodeResult<IBlobWrapper>> TryGetBlobAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline, CancellationToken cancellationToken = default);
}

public interface ISupportsPreSignedUrls : ICacheableBlobPromise
{
    bool TryGeneratePreSignedUrl(IRequestSnapshot request, out string? url);
}

public static class CacheableBlobPromiseExtensions
{
    public static byte[] GetCacheKey32BytesUncached(this ICacheableBlobPromise promise)
    {
        var buffer = new byte[32];
        var used = promise.CopyCacheKeyBytesTo(buffer);
        if (used.Length != 32)
        {
            throw new InvalidOperationException("Hash buffer failure 1125125");
        }
        return buffer;
    }
    public static ReadOnlySpan<byte> CopyCacheKeyBytesTo(this ICacheableBlobPromise promise, Span<byte> buffer32Bytes)
    {
        if (buffer32Bytes.Length < 32)
        {
            throw new ArgumentException("Must be at least 32 bytes", nameof(buffer32Bytes));
        }
        if (!promise.ReadyToWriteCacheKeyBasisData)
        {
            throw new InvalidOperationException("Promise is not ready to write cache key basis data. Did you call RouteDependenciesAsync? Check ReadyToWriteCacheKeyBasisData first");
        }
        using var buffer = new ArrayPoolBufferWriter<byte>(4096);
        promise.WriteCacheKeyBasisPairsToRecursive(buffer);
       
#if NET5_0_OR_GREATER
        var bytesWritten = SHA256.HashData(buffer.WrittenSpan, buffer32Bytes);
buffer.Dispose();
        return buffer32Bytes[..bytesWritten];
#elif NETSTANDARD2_0
        using var hasher = SHA256.Create();
        var segment = buffer.DangerousGetArray();
        var count = buffer.WrittenCount;
        if (count > segment.Count || segment.Array is null)
        {
            throw new InvalidOperationException("Hash buffer failure 1125125");
        }
        var outputBuffer = hasher.ComputeHash(segment.Array, segment.Offset, count);
        outputBuffer.CopyTo(buffer32Bytes);
        return buffer32Bytes[..outputBuffer.Length];
#else
        throw new PlatformNotSupportedException();
#endif
    }
}

public record CacheableBlobPromise(IRequestSnapshot FinalRequest, LatencyTrackingZone LatencyZone, Func<IRequestSnapshot, CancellationToken, ValueTask<CodeResult<IBlobWrapper>>> BlobFunc) : ICacheableBlobPromise{
    
    public bool IsCacheSupporting => true;
    public bool HasDependencies => false;
    public bool ReadyToWriteCacheKeyBasisData => true;
    public bool SupportsPreSignedUrls => false;
    
    private byte[]? cacheKey32Bytes = null;
    public byte[] GetCacheKey32Bytes()
    {
        return cacheKey32Bytes ??= this.GetCacheKey32BytesUncached();
    }
    
    public ValueTask<CodeResult> RouteDependenciesAsync(IBlobRequestRouter router, CancellationToken cancellationToken = default)
    {
        return Tasks.ValueResult(CodeResult.Ok());
    }
    
    public void WriteCacheKeyBasisPairsToRecursive(IBufferWriter<byte> writer)
    {
        FinalRequest.WriteCacheKeyBasisPairsTo(writer);
    }
    
    public async ValueTask<IAdaptableHttpResponse> CreateResponseAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline, CancellationToken cancellationToken = default)
    {
        return new BlobResponse(await BlobFunc(FinalRequest, cancellationToken));
    }
    
    public ValueTask<CodeResult<IBlobWrapper>> TryGetBlobAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline, CancellationToken cancellationToken = default)
    {
        return BlobFunc(FinalRequest, cancellationToken);
    }
}

public record PromiseFuncAsync(IRequestSnapshot FinalRequest, Func<IRequestSnapshot, CancellationToken, ValueTask<IAdaptableHttpResponse>> Func) : IInstantPromise{
    
    public bool IsCacheSupporting => false;
    
    public ValueTask<IAdaptableHttpResponse> CreateResponseAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline, CancellationToken cancellationToken = default)
    {
        return Func(FinalRequest, cancellationToken);
    }
}
public record PromiseFunc(IRequestSnapshot FinalRequest, Func<IRequestSnapshot, IAdaptableHttpResponse> Func) : IInstantPromise{
    
    public bool IsCacheSupporting => false;
    
    public ValueTask<IAdaptableHttpResponse> CreateResponseAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline, CancellationToken cancellationToken = default)
    {
        return Tasks.ValueResult(Func(FinalRequest));
    }
}