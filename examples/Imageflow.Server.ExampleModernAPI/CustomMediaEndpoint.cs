using System.Buffers;
using System.Text;
using System.Text.Json;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Layers;
using Imazen.Routing.Promises;
using Imazen.Routing.Requests;

namespace Imageflow.Server.ExampleModernAPI;


internal record CustomFileData(string Path1, string QueryString1, string Path2, string QueryString2);

/// <summary>
/// This layer will capture requests for .json.custom paths. No .custom file actually exists, but the .json does, and we'll use that to determine the dependencies.
/// </summary>
public class CustomMediaLayer(PathMapper jsonFileMapper) : Imazen.Routing.Layers.IRoutingLayer
{
    public string Name => ".json.custom file handler";

    public IFastCond? FastPreconditions => Conditions.HasPathSuffixOrdinalIgnoreCase(".json.custom");
    public ValueTask<CodeResult<IRoutingEndpoint>?> ApplyRouting(MutableRequest request, CancellationToken cancellationToken = default)
    {
        // FastPreconditions should have already been checked
        var result = jsonFileMapper.TryMapVirtualPath(request.Path.Replace(".json.custom", ".json"));
        if (result == null)
        {
            // no mapping found
            return new ValueTask<CodeResult<IRoutingEndpoint>?>((CodeResult<IRoutingEndpoint>?)null);
        }
        var physicalPath = result.Value.MappedPhysicalPath;
        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(physicalPath);
        if (lastWriteTimeUtc.Year == 1601) // file doesn't exist, pass to next middleware
        {
            return new ValueTask<CodeResult<IRoutingEndpoint>?>((CodeResult<IRoutingEndpoint>?)null);
        }
        // Ok, the file exists. We can load and parse it using System.Text.Json to determine the dependencies.\
        return RouteFromJsonFile(physicalPath, lastWriteTimeUtc, result.Value.MappingUsed, request, cancellationToken);
    }

    private async ValueTask<CodeResult<IRoutingEndpoint>?> RouteFromJsonFile(string jsonFilePath, DateTime lastWriteTimeUtc, IPathMapping mappingUsed, MutableRequest request, CancellationToken cancellationToken)
    {
        // TODO: here, we could cache the json files in memory using a key based on jsonFilePath and lastWriteTimeUtc.
        
        var jsonText = await File.ReadAllTextAsync(jsonFilePath, cancellationToken);
        var data = JsonSerializer.Deserialize<CustomFileData>(jsonText);
        if (data == null)
        {
            return CodeResult<IRoutingEndpoint>.Err((HttpStatus.ServerError, "Failed to parse .json custom data file"));
        }

        return new PromiseWrappingEndpoint(new CustomMediaPromise(request.ToSnapshot(true),data));
    }
}

internal class CustomMediaPromise(IRequestSnapshot r, CustomFileData data) : ICacheableBlobPromise
{
    public bool IsCacheSupporting => true;
    public IRequestSnapshot FinalRequest { get; } = r;

    public async ValueTask<IAdaptableHttpResponse> CreateResponseAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline,
        CancellationToken cancellationToken = default)
    { 
        // This code path isn't called, it's just to satisfy the primitive IInstantPromise interface.
        return new BlobResponse(await TryGetBlobAsync(request, router, pipeline, cancellationToken));
    }

    public bool HasDependencies => true; 
    public bool ReadyToWriteCacheKeyBasisData { get; private set; }

    /// <summary>
    /// Gets a promise for the given path that includes caching logic if indicated by the caching configuration and the latency by default.
    /// </summary>
    /// <param name="router"></param>
    /// <param name="childRequestUri"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async ValueTask<CodeResult<ICacheableBlobPromise>> RouteDependencyAsync(IBlobRequestRouter router, string childRequestUri,
        CancellationToken cancellationToken = default)
    {
        if (FinalRequest.OriginatingRequest == null)
        {
            return CodeResult<ICacheableBlobPromise>.ErrFrom(HttpStatus.BadRequest, "OriginatingRequest is required, but was null");
        }
        var dependencyRequest = MutableRequest.ChildRequest(FinalRequest.OriginatingRequest, FinalRequest, childRequestUri, HttpMethods.Get);
        var routingResult = await router.RouteToPromiseAsync(dependencyRequest, cancellationToken);
        if (routingResult == null)
        {
            return CodeResult<ICacheableBlobPromise>.ErrFrom(HttpStatus.NotFound, "Dependency not found: " + childRequestUri);
        }
        if (routingResult.TryUnwrapError(out var error))
        {
            return CodeResult<ICacheableBlobPromise>.Err(error.WithAppend("Error routing to dependency: " + childRequestUri));
        }
        return CodeResult<ICacheableBlobPromise>.Ok(routingResult.Unwrap());
    }
    public async ValueTask<CodeResult> RouteDependenciesAsync(IBlobRequestRouter router, CancellationToken cancellationToken = default)
    {
        var uri1 = data.Path1 + data.QueryString1;
        var uri2 = data.Path2 + data.QueryString2;
        
        foreach (var uri in new[]{uri1, uri2})
        {
            var routingResult = await RouteDependencyAsync(router, uri, cancellationToken);
            if (routingResult.TryUnwrapError(out var error))
            {
                return CodeResult.Err(error);
            }
            Dependencies ??= new List<ICacheableBlobPromise>();
            Dependencies.Add(routingResult.Unwrap());
        }
        ReadyToWriteCacheKeyBasisData = true;
        return CodeResult.Ok();
    }
    
    internal List<ICacheableBlobPromise>? Dependencies { get; private set; }

    private LatencyTrackingZone? latencyZone = null;
    /// <summary>
    /// Must route dependencies first!
    /// </summary>
    public LatencyTrackingZone? LatencyZone {
        get
        {
            if (!ReadyToWriteCacheKeyBasisData) throw new InvalidOperationException("Dependencies must be routed first");
            // produce a latency zone based on all dependency strings, joined, plus the sum of their latency defaults
            if (latencyZone != null) return latencyZone;
            var latency = 0;
            var sb = new StringBuilder();
            sb.Append("customMediaSwitcher(");
            foreach (var dependency in Dependencies!)
            {
                latency += dependency.LatencyZone?.DefaultMs ?? 0;
                sb.Append(dependency.LatencyZone?.TrackingZone ?? "(unknown)");
            }
            sb.Append(")");
            latencyZone = new LatencyTrackingZone(sb.ToString(), latency, true); //AlwaysShield is true (never skip caching)
            return latencyZone;
        }
    }

    public void WriteCacheKeyBasisPairsToRecursive(IBufferWriter<byte> writer)
    {
        FinalRequest.WriteCacheKeyBasisPairsTo(writer);
        if (Dependencies == null) throw new InvalidOperationException("Dependencies must be routed first");
        foreach (var dependency in Dependencies)
        {
            dependency.WriteCacheKeyBasisPairsToRecursive(writer);
        }

        var otherCacheKeyData = 1;
        writer.WriteInt(otherCacheKeyData);
    }

    private byte[]? cacheKey32Bytes = null;
    public byte[] GetCacheKey32Bytes()
    {
        return cacheKey32Bytes ??= this.GetCacheKey32BytesUncached();
    }

    public bool SupportsPreSignedUrls => false;

    public async ValueTask<CodeResult<IBlobWrapper>> TryGetBlobAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline,
        CancellationToken cancellationToken = default)
    {
        // Our logic is to return whichever dependency is smaller. 
        // This is a contrived example, but it's a good example of how to use dependencies.
        var blobWrappers = new List<IBlobWrapper>();
        var smallestBlob = default(IBlobWrapper);
        try
        {
            foreach (var dependency in Dependencies!)
            {
                var result = await dependency.TryGetBlobAsync(request, router, pipeline, cancellationToken);
                if (result.TryUnwrapError(out var error))
                {
                    return CodeResult<IBlobWrapper>.Err(error);
                }
                var blob = result.Unwrap();
                blobWrappers.Add(blob);
                
                if (smallestBlob == null || blob.Attributes.EstimatedBlobByteCount < smallestBlob.Attributes.EstimatedBlobByteCount)
                {
                    smallestBlob = blob;
                }
            }
            if (smallestBlob == null)
            {
                return CodeResult<IBlobWrapper>.ErrFrom(HttpStatus.NotFound, "No dependencies found");
            }
            return CodeResult<IBlobWrapper>.Ok(smallestBlob.ForkReference());
        }
        finally
        {
            foreach (var blobWrapper in blobWrappers)
            {
                blobWrapper.Dispose();
            }
        }
    }
}
    
