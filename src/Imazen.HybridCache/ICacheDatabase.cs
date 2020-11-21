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
        Task UpdateLastDeletionAttempt(string relativePath, DateTime when);


        /// <summary>
        /// True if the record exists, false if it is already gone
        /// </summary>
        /// <returns></returns>
        Task<bool> DeleteRecord(string relativePath);

        /// <summary>
        /// Return the oldest (by created date) records, sorted from oldest to newest, where
        /// the last deletion attempt is older than maxLastDeletionAttemptTime and the
        /// created date is older than maxCreatedDate
        /// </summary>
        /// <param name="maxLastDeletionAttemptTime"></param>
        /// <param name="maxCreatedDate"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        Task<IEnumerable<ICacheDatabaseRecord>> GetOldestRecords(DateTime maxLastDeletionAttemptTime, DateTime maxCreatedDate, int count);
        
        Task<long> GetTotalBytes();


        /// <summary>
        /// Looks up the content type value by key
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        Task<string> GetContentType(string relativePath);
        
        /// <summary>
        /// Estimate disk usage for a record with the given key length
        /// </summary>
        /// <param name="stringKeyLength"></param>
        /// <returns></returns>
        int EstimateRecordDiskSpace(int stringKeyLength);

        /// <summary>
        /// Within a lock or transaction, checks if Sum(DiskBytes) + recordDiskSpace &lt; diskSpaceLimit, then
        /// creates a new record with the given key, content-type, record disk space, createdDate
        /// and last deletion attempt time set to the lowest possible value
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="contentType"></param>
        /// <param name="recordDiskSpace"></param>
        /// <param name="createdDate"></param>
        /// <param name="accessCountKey"></param>
        /// <param name="diskSpaceLimit"></param>
        /// <returns></returns>
        Task<bool> CreateRecordIfSpace(string relativePath, string contentType, long recordDiskSpace, DateTime createdDate, int accessCountKey, long diskSpaceLimit);

        Task UpdateCreatedDate(string relativePath, DateTime createdDate);
        
        /// <summary>
        /// May require creation of new record and deletion of old, since we are changing the primary key
        /// 
        /// </summary>
        /// <param name="record"></param>
        /// <param name="movedRelativePath"></param>
        /// <param name="lastDeletionAttempt"></param>
        /// <returns></returns>
        Task ReplaceRelativePathAndUpdateLastDeletion(ICacheDatabaseRecord record, string movedRelativePath, DateTime lastDeletionAttempt);
    }
}