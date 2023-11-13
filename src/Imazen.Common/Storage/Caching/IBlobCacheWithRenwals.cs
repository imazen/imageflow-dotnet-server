using System.Threading;
using System.Threading.Tasks;

namespace Imazen.Common.Storage.Caching
{
    /// <summary>
    /// Useful if your blob storage provider requires manual renewal of blobs to keep them from being evicted. Use with ICacheBlobDataExpiry.
    /// </summary>
    public interface IBlobCacheWithRenewals : IBlobCache
    {
        Task<ICacheBlobPutResult> TryRenew(BlobGroup group, string key, CancellationToken cancellationToken = default);
    }
}