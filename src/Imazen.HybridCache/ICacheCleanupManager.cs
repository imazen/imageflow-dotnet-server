using System;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Concurrency;

namespace Imazen.HybridCache
{
    internal struct ReserveSpaceResult
    {
        internal bool Success { get; set; }
        internal string Message { get; set; }
    }
    internal interface ICacheCleanupManager
    {
        void NotifyUsed(CacheEntry cacheEntry);
        Task<string> GetContentType(CacheEntry cacheEntry, CancellationToken cancellationToken);
        Task<ICacheDatabaseRecord> GetRecordReference(CacheEntry cacheEntry, CancellationToken cancellationToken);
        Task<ReserveSpaceResult> TryReserveSpace(CacheEntry cacheEntry, string contentType, int byteCount, bool allowEviction, AsyncLockProvider writeLocks, CancellationToken cancellationToken);

        Task MarkFileCreated(CacheEntry cacheEntry, string contentType, long recordDiskSpace, DateTime createdDate);
    }
}