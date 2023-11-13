using System;

namespace Imazen.Common.Storage.Caching
{
     /// <summary>
    /// Useful if your blob storage provider requires manual renewal of blobs to keep them from being evicted. Use with IBlobCacheWithRenewals.
    /// </summary>
    public interface ICacheBlobDataExpiry
    {
        DateTimeOffset? EstimatedExpiry { get; }
    }
}
