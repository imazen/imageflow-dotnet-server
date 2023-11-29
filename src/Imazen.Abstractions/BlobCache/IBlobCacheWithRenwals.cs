using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;

namespace Imazen.Abstractions.BlobCache
{
    /// <summary>
    /// Useful if your blob storage provider requires manual renewal of blobs to keep them from being evicted.
    /// </summary>
    [Obsolete]
    public interface IBlobCacheWithRenewals : IBlobCache
    {
        Task<CodeResult> CacheRenewEntry(IBlobStorageReference entry, CancellationToken cancellationToken = default);
    }
}