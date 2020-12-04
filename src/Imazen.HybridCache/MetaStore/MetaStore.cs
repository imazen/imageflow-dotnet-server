using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Imazen.HybridCache.MetaStore
{
    public class MetaStore: ICacheDatabase
    {
        private MetaStoreOptions Options { get; }
        private ILogger Logger { get;  }

        private readonly Shard[] shards;


        public MetaStore(MetaStoreOptions options, ILogger logger)
        {
            Options = options;
            Logger = logger;
            if (options.Shards <= 0 || options.Shards > 2048)
            {
                throw new ArgumentException("Shards must be between 1 and 2048");
            }

            var shardCount = options.Shards;
            shards = new Shard[shardCount];
            for (var i = 0; i < shardCount; i++)
            {
                shards[i] = new Shard(i, options, Path.Combine(options.DatabaseDir,"db", i.ToString()), logger);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.WhenAll(shards.Select(s => s.StartAsync(cancellationToken)));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.WhenAll(shards.Select(s => s.StopAsync(cancellationToken)));
        }

        public Task UpdateLastDeletionAttempt(int shard, string relativePath, DateTime when)
        {
            return shards[shard].UpdateLastDeletionAttempt(relativePath, when);
        }

        public int GetShardForKey(string key)
        {
            var stringBytes = Encoding.UTF8.GetBytes(key);
            
            using (var h = SHA256.Create())
            {
                var a = h.ComputeHash(stringBytes);
                var shardSeed = BitConverter.ToUInt32(a, 8);

                var shard = (int)(shardSeed % shards.Length);
                return shard;
            }
        }

        public Task DeleteRecord(int shard, ICacheDatabaseRecord record, bool fileDeleted)
        {
            if (record.CreatedAt > DateTime.UtcNow)
                throw new InvalidOperationException();
            return shards[shard].DeleteRecord(record, fileDeleted);
        }

        public Task<IEnumerable<ICacheDatabaseRecord>> GetDeletionCandidates(int shard, DateTime maxLastDeletionAttemptTime, DateTime maxCreatedDate, int count,
            Func<int, ushort> getUsageCount)
        {
            return shards[shard]
                .GetDeletionCandidates(maxLastDeletionAttemptTime, maxCreatedDate, count, getUsageCount);
        }

        public Task<long> GetShardSize(int shard)
        {
            return shards[shard].GetShardSize();
        }

        public int GetShardCount()
        {
            return shards.Length;
        }

        public Task<string> GetContentType(int shard, string relativePath)
        {
            return shards[shard].GetContentType(relativePath);
        }

        public int EstimateRecordDiskSpace(int stringLength)
        {
            return Shard.GetLogBytesOverhead(stringLength);
        }

        public Task<bool> CreateRecordIfSpace(int shard, string relativePath, string contentType, long recordDiskSpace, DateTime createdDate,
            int accessCountKey, long diskSpaceLimit)
        {
            return shards[shard].CreateRecordIfSpace(relativePath, contentType, recordDiskSpace, createdDate,
                accessCountKey, diskSpaceLimit);
        }

        public Task UpdateCreatedDateAtomic(int shard, string relativePath, string contentType, long recordDiskSpace, DateTime createdDate, int accessCountKey)
        {
            return shards[shard].UpdateCreatedDate(relativePath, contentType, recordDiskSpace, createdDate, accessCountKey);
        }

        public Task ReplaceRelativePathAndUpdateLastDeletion(int shard, ICacheDatabaseRecord record, string movedRelativePath,
            DateTime lastDeletionAttempt)
        {
            return shards[shard].ReplaceRelativePathAndUpdateLastDeletion(record, movedRelativePath, lastDeletionAttempt);
        }
    }
}