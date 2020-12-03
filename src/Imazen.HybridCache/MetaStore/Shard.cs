using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Imazen.HybridCache.Sqlite;
using Microsoft.Extensions.Logging;
using NeoSmart.AsyncLock;

namespace Imazen.HybridCache.MetaStore
{
    internal class Shard
    {
        private readonly MetaStoreOptions options;
        private readonly string databaseDir;
        
        private readonly WriteLog writeLog;
        private readonly ILogger logger;
        private readonly int shardId;
        internal Shard(int shardId,MetaStoreOptions options, string databaseDir,  ILogger logger)
        {
            this.shardId = shardId;
            this.options = options;
            this.databaseDir = databaseDir;
            this.logger = logger;
            writeLog = new WriteLog(databaseDir, options, logger);
        }

        private volatile ConcurrentDictionary<string, CacheDatabaseRecord> dict;

        private readonly AsyncLock readLock = new AsyncLock();
        private async Task<ConcurrentDictionary<string, CacheDatabaseRecord>> GetLoadedDict()
        {
            if (dict != null) return dict;
            using (var readLocked = await readLock.LockAsync())
            {
                if (dict != null) return dict;

                dict = await writeLog.Startup();
            }
            return dict;
        }

        private long cacheSize;
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return writeLog.StopAsync(cancellationToken);
        }

        public async Task UpdateLastDeletionAttempt(string relativePath, DateTime when)
        {
            if ((await GetLoadedDict()).TryGetValue(relativePath, out var record))
            {
                record.LastDeletionAttempt = when;
            }
        }
        
        public async Task DeleteRecord(ICacheDatabaseRecord record, bool fileDeleted)
        {
           
            if ((await GetLoadedDict()).TryRemove(record.RelativePath, out var unused))
            {
                await writeLog.LogDeleted(record);
                Interlocked.Add(ref cacheSize, -record.DiskSize);
                //logger?.LogInformation("Decreasing cacheSize to {CacheSize}", cacheSize);
            }
        }

        public async Task<IEnumerable<ICacheDatabaseRecord>> GetDeletionCandidates(
            DateTime maxLastDeletionAttemptTime, DateTime maxCreatedDate, int count, Func<int, ushort> getUsageCount)
        {
            var results = (await GetLoadedDict()).Values
                .Where(r => r.CreatedAt < maxCreatedDate && r.LastDeletionAttempt < maxLastDeletionAttemptTime)
                .Select(r => new Tuple<CacheDatabaseRecord, ushort>(r, getUsageCount(r.AccessCountKey)))
                .OrderByDescending(t => t.Item2)
                .Select(t => (ICacheDatabaseRecord) t.Item1)
                .Take(count).ToArray();
            logger?.LogInformation("Found {DeletionCandidates} deletion candidates in MetaStore", results.Length);
            return results;
        }

        public Task<long> GetShardSize()
        {
            return Task.FromResult(cacheSize);
        }
        public async Task<string> GetContentType(string relativePath)
        {
            return (await GetLoadedDict()).TryGetValue(relativePath, out var record) ? 
                record.ContentType : 
                null;
        }


        public async Task<bool> CreateRecordIfSpace(string relativePath, string contentType, long recordDiskSpace,
            DateTime createdDate,
            int accessCountKey, long diskSpaceLimit)
        {
            if (cacheSize + recordDiskSpace > diskSpaceLimit)
            {
                logger?.LogInformation("Refusing new {RecordDiskSpace} byte record in MetaStore", recordDiskSpace);
                return false;
            }

            var newRecord = new CacheDatabaseRecord()
            {
                AccessCountKey = accessCountKey,
                ContentType = contentType,
                CreatedAt = createdDate,
                DiskSize = recordDiskSpace,
                LastDeletionAttempt = DateTime.MinValue,
                RelativePath = relativePath
            };

            if ((await GetLoadedDict()).TryAdd(relativePath, newRecord))
            {
                await writeLog.LogCreated(newRecord);
                Interlocked.Add(ref cacheSize, recordDiskSpace);

                //logger?.LogInformation("Increasing cacheSize to {CacheSize}", cacheSize);
            }

            return true;

        }

        public async Task UpdateCreatedDate(string relativePath, DateTime createdDate)
        {
            if ((await GetLoadedDict()).TryGetValue(relativePath, out var record))
            {
                record.CreatedAt = createdDate;
                await writeLog.LogUpdated(record);
            }
        }

        public async Task ReplaceRelativePathAndUpdateLastDeletion(ICacheDatabaseRecord record,
            string movedRelativePath,
            DateTime lastDeletionAttempt)
        {
            var newRecord = new CacheDatabaseRecord()
            {
                AccessCountKey = record.AccessCountKey,
                ContentType = record.ContentType,
                CreatedAt = record.CreatedAt,
                DiskSize = record.DiskSize,
                LastDeletionAttempt = lastDeletionAttempt,
                RelativePath = movedRelativePath
            };
            
            var loadedDict = await GetLoadedDict();
            if (loadedDict.TryAdd(movedRelativePath, newRecord))
            {
                await writeLog.LogCreated(newRecord);
            }
            if (loadedDict.TryRemove(record.RelativePath, out var oldRecord))
            {
                await writeLog.LogDeleted(oldRecord);
            }
        }
    }
}