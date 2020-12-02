using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Imazen.HybridCache.Sqlite;

namespace Imazen.HybridCache.MetaStore
{
    public class MetaStore: ICacheDatabase
    {
        private readonly ConcurrentDictionary<string, CacheDatabaseRecord> dict =
            new ConcurrentDictionary<string, CacheDatabaseRecord>(StringComparer.Ordinal);

        private long cacheSize;
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UpdateLastDeletionAttempt(string relativePath, DateTime when)
        {
            if (dict.TryGetValue(relativePath, out var record))
            {
                record.LastDeletionAttempt = when;
            }
            return Task.CompletedTask;
        }

        public Task DeleteRecord(ICacheDatabaseRecord record, bool fileDeleted)
        {
           
            if (dict.TryRemove(record.RelativePath, out var unused))
            {
                Interlocked.Add(ref cacheSize, -record.DiskSize);
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<ICacheDatabaseRecord>> GetDeletionCandidates(DateTime maxLastDeletionAttemptTime, DateTime maxCreatedDate, int count, Func<int, ushort> getUsageCount)
        {
            return Task.FromResult(dict.Values
                .Where(r => r.CreatedAt < maxCreatedDate && r.LastDeletionAttempt < maxLastDeletionAttemptTime)
                .Select(r => new Tuple<CacheDatabaseRecord, ushort>(r, getUsageCount(r.AccessCountKey)))
                .OrderByDescending(t => t.Item2)
                .Select(t => (ICacheDatabaseRecord)t.Item1)
                .Take(count));
        }

        public Task<long> GetTotalBytes()
        {
            return Task.FromResult(cacheSize);
        }

        public Task<string> GetContentType(string relativePath)
        {
            return dict.TryGetValue(relativePath, out var record) ? 
                Task.FromResult(record.ContentType) : 
                Task.FromResult<string>(null);
        }

        public int EstimateRecordDiskSpace(int stringKeyLength)
        {
            return 128 + stringKeyLength * 2;
        }

        public Task<bool> CreateRecordIfSpace(string relativePath, string contentType, long recordDiskSpace, DateTime createdDate,
            int accessCountKey, long diskSpaceLimit)
        {
            if (cacheSize + recordDiskSpace > diskSpaceLimit)
            {
                return Task.FromResult(false);
            }

            if (dict.TryAdd(relativePath, new CacheDatabaseRecord()
                {
                    AccessCountKey = accessCountKey,
                    ContentType = contentType,
                    CreatedAt = createdDate,
                    DiskSize = recordDiskSpace,
                    LastDeletionAttempt = DateTime.MinValue,
                    RelativePath = relativePath
                }))
                {
                    Interlocked.Add(ref cacheSize, recordDiskSpace);
                }
                return Task.FromResult(true);;
            
        }

        public Task UpdateCreatedDate(string relativePath, DateTime createdDate)
        {
            if (dict.TryGetValue(relativePath, out var record))
            {
                record.CreatedAt = createdDate;
            }
            return Task.CompletedTask;
        }

        public Task ReplaceRelativePathAndUpdateLastDeletion(ICacheDatabaseRecord record, string movedRelativePath,
            DateTime lastDeletionAttempt)
        {
            dict.TryAdd(movedRelativePath, new CacheDatabaseRecord()
            {
                AccessCountKey = record.AccessCountKey,
                ContentType = record.ContentType,
                CreatedAt = record.CreatedAt,
                DiskSize = record.DiskSize,
                LastDeletionAttempt = lastDeletionAttempt,
                RelativePath = movedRelativePath
            });
            dict.TryRemove(record.RelativePath, out var unused);
            return Task.CompletedTask;
        }
    }
}