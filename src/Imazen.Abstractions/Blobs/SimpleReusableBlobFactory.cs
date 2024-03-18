using System.Diagnostics;
using System.Net.Http.Headers;

namespace Imazen.Abstractions.Blobs;

/// <summary>
/// Implementations should be thread-safe and disposable. Since this will be (the?) main source
/// for long-lived large allocations, various strategies can be implemented - such as a custom
/// read-only stream backed by a pool of memory chunks. When all streams to the data are disposed, the
/// chunks could be released into a pool for reuse. 
/// </summary>
public interface IReusableBlobFactory : IDisposable
{
 
}
/// <summary>
/// This could utilize different backing stores such as a memory pool, releasing blobs to the pool when they (and all their created streams)
/// are disposed
/// </summary>
public class SimpleReusableBlobFactory: IReusableBlobFactory
{
    public async ValueTask<IReusableBlob> ConsumeAndCreateReusableCopy(IConsumableBlob consumableBlob,
        CancellationToken cancellationToken = default)
    {
        using (consumableBlob)
        {
            var sw = Stopwatch.StartNew();
#if NETSTANDARD2_1_OR_GREATER 
             await using var stream = consumableBlob.BorrowStream(DisposalPromise.CallerDisposesStreamThenBlob);
#else
            using var stream = consumableBlob.BorrowStream(DisposalPromise.CallerDisposesStreamThenBlob);
#endif
            var ms = new MemoryStream(stream.CanSeek ? (int)stream.Length : 4096);
            await stream.CopyToAsync(ms, 81920, cancellationToken);
            ms.Position = 0;
            var byteArray = ms.ToArray();
            var arraySegment = new ArraySegment<byte>(byteArray);
            sw.Stop();
            var reusable = new MemoryBlob(arraySegment, consumableBlob.Attributes, sw.Elapsed);
            return reusable;
        }
    }

    public void Dispose()
    {
        // MemoryStreams have no unmanaged resources, and the garbage collector is optimal for them.
    }
}