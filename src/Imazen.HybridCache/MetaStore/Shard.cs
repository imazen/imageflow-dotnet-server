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
        
        private volatile ConcurrentDictionary<string, CacheDatabaseRecord> dict;

        private readonly AsyncLock readLock = new AsyncLock();
        private readonly AsyncLock createLock = new AsyncLock();
        internal Shard(int shardId, MetaStoreOptions options, string databaseDir,  ILogger logger)
        {
            this.shardId = shardId;
            this.options = options;
            this.databaseDir = databaseDir;
            this.logger = logger;
            writeLog = new WriteLog(shardId, databaseDir, options, logger);
        }


        private async Task<ConcurrentDictionary<string, CacheDatabaseRecord>> GetLoadedDict()
        {
            if (dict != null) return dict;
            using (var unused = await readLock.LockAsync())
            {
                if (dict != null) return dict;

                dict = await writeLog.Startup();
            }
            return dict;
        }
        
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
            //logger?.LogInformation("Found {DeletionCandidates} deletion candidates in shard {ShardId} of MetaStore", results.Length, shardId);
            return results;
        }

        public async Task<long> GetShardSize()
        { 
            await GetLoadedDict();
            return writeLog.GetDiskSize();
        }
        public async Task<string> GetContentType(string relativePath)
        {
            return (await GetLoadedDict()).TryGetValue(relativePath, out var record) ? 
                record.ContentType : 
                null;
        }


        internal static int GetLogBytesOverhead(int stringLength)
        {
            return 4 * (128 + stringLength);
        }
        public async Task<bool> CreateRecordIfSpace(string relativePath, string contentType, long recordDiskSpace,
            DateTime createdDate,
            int accessCountKey, long diskSpaceLimit)
        {
            // Lock so multiple writers can't get past the capacity check simultaneously
            using (await createLock.LockAsync())
            {
                var loadedDict = await GetLoadedDict();
                var extraLogBytes = GetLogBytesOverhead(relativePath.Length + contentType.Length);

                var existingDiskUsage = await GetShardSize();
                if (existingDiskUsage + recordDiskSpace + extraLogBytes > diskSpaceLimit)
                {
                    //logger?.LogInformation("Refusing new {RecordDiskSpace} byte record in shard {ShardId} of MetaStore",
                    //    recordDiskSpace, shardId);
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

                if (loadedDict.TryAdd(relativePath, newRecord))
                {
                    await writeLog.LogCreated(newRecord);

                }
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