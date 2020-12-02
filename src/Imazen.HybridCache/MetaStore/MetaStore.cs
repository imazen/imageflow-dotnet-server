using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Imazen.HybridCache.Sqlite;
using Microsoft.Extensions.Logging;

namespace Imazen.HybridCache.MetaStore
{
    public class MetaStore: ICacheDatabase
    {
        private ILogger Logger { get;  }

        public MetaStore(ILogger logger)
        {
            Logger = logger;
        }
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

        public Task UpdateLastDeletionAttempt(int shard, string relativePath, DateTime when)
        {
            if (dict.TryGetValue(relativePath, out var record))
            {
                record.LastDeletionAttempt = when;
            }
            return Task.CompletedTask;
        }

        public int GetShardForKey(string key)
        {
            return 0;
        }

        public Task DeleteRecord(int shard, ICacheDatabaseRecord record, bool fileDeleted)
        {
           
            if (dict.TryRemove(record.RelativePath, out var unused))
            {
                Interlocked.Add(ref cacheSize, -record.DiskSize);
                Logger?.LogInformation("Decreasing cacheSize to {CacheSize}", cacheSize);
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<ICacheDatabaseRecord>> GetDeletionCandidates(int shard,
            DateTime maxLastDeletionAttemptTime, DateTime maxCreatedDate, int count, Func<int, ushort> getUsageCount)
        {
            var results = dict.Values
                .Where(r => r.CreatedAt < maxCreatedDate && r.LastDeletionAttempt < maxLastDeletionAttemptTime)
                .Select(r => new Tuple<CacheDatabaseRecord, ushort>(r, getUsageCount(r.AccessCountKey)))
                .OrderByDescending(t => t.Item2)
                .Select(t => (ICacheDatabaseRecord) t.Item1)
                .Take(count).ToArray();
            Logger?.LogInformation("Found {DeletionCandidates} deletion candidates in MetaStore", results.Length);
            return Task.FromResult((IEnumerable<ICacheDatabaseRecord>)results);
        }

        public Task<long> GetShardSize(int shard)
        {
            return Task.FromResult(cacheSize);
        }

        public int GetShardCount()
        {
            return 1;
        }

        public Task<string> GetContentType(int shard, string relativePath)
        {
            return dict.TryGetValue(relativePath, out var record) ? 
                Task.FromResult(record.ContentType) : 
                Task.FromResult<string>(null);
        }

        public int EstimateRecordDiskSpace(int stringKeyLength)
        {
            return 128 + stringKeyLength * 2;
        }

        public Task<bool> CreateRecordIfSpace(int shard, string relativePath, string contentType, long recordDiskSpace,
            DateTime createdDate,
            int accessCountKey, long diskSpaceLimit)
        {
            if (cacheSize + recordDiskSpace > diskSpaceLimit)
            {
                Logger?.LogInformation("Refusing new {RecordDiskSpace} byte record in MetaStore", recordDiskSpace);
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
                    
                    Logger?.LogInformation("Increasing cacheSize to {CacheSize}", cacheSize);
                }
                return Task.FromResult(true);;
            
        }

        public Task UpdateCreatedDate(int shard, string relativePath, DateTime createdDate)
        {
            if (dict.TryGetValue(relativePath, out var record))
            {
                record.CreatedAt = createdDate;
            }
            return Task.CompletedTask;
        }

        public Task ReplaceRelativePathAndUpdateLastDeletion(int shard, ICacheDatabaseRecord record,
            string movedRelativePath,
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