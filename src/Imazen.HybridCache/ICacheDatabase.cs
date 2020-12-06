using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Imazen.HybridCache
{
    public interface ICacheDatabaseRecord
    {
        int AccessCountKey { get; }
        DateTime CreatedAt { get; }
        DateTime LastDeletionAttempt { get;  }
        long DiskSize { get; }
        string RelativePath { get; }
        string ContentType { get; }
    }
    public interface ICacheDatabase: IHostedService
    {
        Task UpdateLastDeletionAttempt(int shard, string relativePath, DateTime when);


        int GetShardForKey(string key);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="record"></param>
        /// <returns></returns>
        Task DeleteRecord(int shard, ICacheDatabaseRecord record);

        /// <summary>
        /// Return the oldest (by created date) records, sorted from oldest to newest, where
        /// the last deletion attempt is older than maxLastDeletionAttemptTime and the
        /// created date is older than maxCreatedDate
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="maxLastDeletionAttemptTime"></param>
        /// <param name="maxCreatedDate"></param>
        /// <param name="count"></param>
        /// <param name="getUsageCount"></param>
        /// <returns></returns>
        Task<IEnumerable<ICacheDatabaseRecord>> GetDeletionCandidates(int shard, DateTime maxLastDeletionAttemptTime,
            DateTime maxCreatedDate, int count, Func<int, ushort> getUsageCount);
        
        Task<long> GetShardSize(int shard);

        int GetShardCount();

        /// <summary>
        /// Looks up the content type value by key
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        Task<string> GetContentType(int shard, string relativePath);
        
        /// <summary>
        /// Estimate disk usage for a record with the given key length
        /// </summary>
        /// <param name="stringLength"></param>
        /// <returns></returns>
        int EstimateRecordDiskSpace(int stringLength);

        /// <summary>
        /// Within a lock or transaction, checks if Sum(DiskBytes) + recordDiskSpace &lt; diskSpaceLimit, then
        /// creates a new record with the given key, content-type, record disk space, createdDate
        /// and last deletion attempt time set to the lowest possible value
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="relativePath"></param>
        /// <param name="contentType"></param>
        /// <param name="recordDiskSpace"></param>
        /// <param name="createdDate"></param>
        /// <param name="accessCountKey"></param>
        /// <param name="diskSpaceLimit"></param>
        /// <returns></returns>
        Task<bool> CreateRecordIfSpace(int shard, string relativePath, string contentType, long recordDiskSpace, DateTime createdDate, int accessCountKey, long diskSpaceLimit);

        /// <summary>
        /// Should only delete the record if the createdDate matches
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="relativePath"></param>
        /// <param name="contentType"></param>
        /// <param name="recordDiskSpace"></param>
        /// <param name="createdDate"></param>
        /// <param name="accessCountKey"></param>
        /// <returns></returns>
        Task UpdateCreatedDateAtomic(int shard, string relativePath, string contentType, long recordDiskSpace, DateTime createdDate, int accessCountKey);

        /// <summary>
        /// May require creation of new record and deletion of old, since we are changing the primary key
        /// 
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="record"></param>
        /// <param name="movedRelativePath"></param>
        /// <param name="lastDeletionAttempt"></param>
        /// <returns></returns>
        Task ReplaceRelativePathAndUpdateLastDeletion(int shard, ICacheDatabaseRecord record, string movedRelativePath, DateTime lastDeletionAttempt);
    }
}