using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;
using Imazen.HybridCache.MetaStore;
using Microsoft.Extensions.Hosting;

namespace Imazen.HybridCache
{
    [Flags]
    internal enum CacheEntryFlags : byte
    {
        Unknown = 0,
        Proxied = 1,
        Generated = 2,
        Metadata = 4,
        DoNotEvict = 128
    }


    internal interface ICacheDatabase<T>: IHostedService where T:ICacheDatabaseRecord
    {
        Task UpdateLastDeletionAttempt(int shard, string relativePath, DateTime when);


        int GetShardForKey(string key);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="record"></param>
        /// <returns></returns>
        Task<DeleteRecordResult> DeleteRecord(int shard, T record);

        Task<CodeResult> TestRootDirectory();

        /// <summary>
        /// Return the oldest (by created date) records, sorted from oldest to newest, where
        /// the last deletion attempt is older than maxLastDeletionAttemptTime and the
        /// created date is older than maxCreatedDate. 
        /// Possible issues if records are modified
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="maxLastDeletionAttemptTime"></param>
        /// <param name="maxCreatedDate"></param>
        /// <param name="count"></param>
        /// <param name="getUsageCount"></param>
        /// <returns></returns>
        Task<IEnumerable<T>> GetDeletionCandidates(int shard, DateTime maxLastDeletionAttemptTime,
            DateTime maxCreatedDate, int count, Func<int, ushort> getUsageCount);
        
        Task<IEnumerable<T>> LinearSearchByTag(int shard, SearchableBlobTag tag);

        
        Task<long> GetShardSize(int shard);

        int GetShardCount();


        /// <summary>
        /// Looks up the record info by key
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="relativePath"></param>
        Task<T?> GetRecord(int shard, string relativePath);

        /// <summary>
        /// Estimate disk usage for the given record
        /// </summary>
        /// <returns></returns>
        int EstimateRecordDiskSpace(CacheDatabaseRecord newRecord);

        /// <summary>
        /// Within a lock or transaction, checks if Sum(DiskBytes) + recordDiskSpace &lt; diskSpaceLimit, then
        /// creates a new record with the given key, content-type, record disk space, createdDate
        /// and last deletion attempt time set to the lowest possible value
        /// </summary>
        /// 
        /// <returns></returns>
        Task<bool> CreateRecordIfSpace(int shard, CacheDatabaseRecord newRecord, long diskSpaceLimit);


        Task UpdateCreatedDateAtomic(int shard, string relativePath, DateTime createdDate,
            Func<CacheDatabaseRecord> createIfMissing);

        /// <summary>
        /// May require creation of new record and deletion of old, since we are changing the primary key
        /// 
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="record"></param>
        /// <param name="movedRelativePath"></param>
        /// <param name="lastDeletionAttempt"></param>
        /// <returns></returns>
        Task ReplaceRelativePathAndUpdateLastDeletion(int shard, T record, string movedRelativePath, DateTime lastDeletionAttempt);

        Task<CodeResult> TestMetaStore();
    }
}