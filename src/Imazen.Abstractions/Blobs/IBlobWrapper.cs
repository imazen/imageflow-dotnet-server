using Imazen.Common.Extensibility.Support;
using Microsoft.Extensions.Logging;

namespace Imazen.Abstractions.Blobs;

/// <summary>
/// Wraps a consumable blob, tracking references and buffering it to memory if necessary.
/// When the last wrapper is disposed, the underlying blob is disposed.
/// </summary>
public interface IBlobWrapper : IDisposable //TODO:  Change to IAsyncDisposable if anyone needs that (network streams)?
{
    IBlobAttributes Attributes { get; }
                
    long? EstimateAllocatedBytes { get; }

    bool IsReusable { get; }
    
    ValueTask EnsureReusable(CancellationToken cancellationToken = default);
    
    void IndicateInterest();
    
    IConsumableBlobPromise GetConsumablePromise();
    IConsumableMemoryBlobPromise GetConsumableMemoryPromise();

    IBlobWrapper ForkReference();
}
// This design simplifies the API but doesn't resolve the issue of needing to dispose the blob when all consumers are done with it.
// 
//
// using Imazen.Common.Extensibility.Support;
// using Microsoft.Extensions.Logging;
//
// namespace Imazen.Abstractions.Blobs;
//
// public enum BlobWrapperUsage
// {
//     ImageDecoding,
//     MemoryCaching,
//     AsyncCaching,
//     Streaming,
//     PipeWriting
// }
//
// public enum FutureUsageRegistrationResult
// {
//     AlreadyRegistered,
//     Registered,
//     Disposed
// }
// /// <summary>
// /// Wraps a consumable blob, tracking references and buffering it to memory if necessary.
// /// When the last wrapper is disposed, the underlying blob is disposed.
// /// </summary>
// public interface IBlobWrapper : IDisposable //TODO:  Change to IAsyncDisposable if anyone needs that (network streams)?
// {
//     IBlobAttributes Attributes { get; }
//                 
//     long? EstimateAllocatedBytes { get; }
//
//     bool IsBuffered { get; }

//     /// <summary>
//     /// Call this once for each future usage of GetConsumableBlob or GetConsumableMemoryBlob.
//     /// Returns true if BufferAsync still needs be called to ensure the blob is buffered to memory.
//     /// </summary>
//     /// <param name="usage"></param>
//     bool RegisterFutureUsage(BlobWrapperUsage usage);
//     
//     ValueTask BufferAsync(CancellationToken cancellationToken = default);
//     
//     /// <summary>
//     /// You must call RegisterFutureUsage for each future usage scenario before calling this or any other Get Method. 
//     /// </summary>
//     /// <returns></returns>
//     ValueTask<IConsumableBlob> GetConsumableBlob();
//     /// <summary>
//     /// You must call RegisterFutureUsage for each future usage scenario before calling this or any other Get Method.
//     /// </summary>
//     /// <returns></returns>
//     ValueTask<IConsumableMemoryBlob> GetMemoryBlob();
// }