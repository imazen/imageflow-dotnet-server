using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;
using Imazen.Common.Concurrency;
using Imazen.HybridCache.MetaStore;

namespace Imazen.HybridCache
{
    internal struct ReserveSpaceResult
    {
        internal bool Success { get; set; }
        internal string Message { get; set; }
    }
    internal interface ICacheCleanupManager
    {
        long EstimateFileSizeOnDisk(long byteCount);
        void NotifyUsed(CacheEntry cacheEntry);
        Task<ICacheDatabaseRecord?> GetRecordReference(CacheEntry cacheEntry, CancellationToken cancellationToken);
        Task<ReserveSpaceResult> TryReserveSpace(CacheEntry cacheEntry, CacheDatabaseRecord newRecord, bool allowEviction, AsyncLockProvider writeLocks, CancellationToken cancellationToken);

        int GetAccessCountKey(CacheEntry cacheEntry);
        
        Task MarkFileCreated(CacheEntry cacheEntry, DateTime createdDate, Func<CacheDatabaseRecord> createIfMissing);
        
        Task<CodeResult<IAsyncEnumerable<IBlobStorageReference>>> CacheSearchByTag(SearchableBlobTag tag, CancellationToken cancellationToken = default);
        Task<CodeResult<IAsyncEnumerable<CodeResult<IBlobStorageReference>>>> CachePurgeByTag(SearchableBlobTag tag, AsyncLockProvider writeLocks, CancellationToken cancellationToken = default);

        Task<CodeResult> CacheDelete(string relativePath, AsyncLockProvider writeLocks,
            CancellationToken cancellationToken = default);
    }
}