using System.Security.Cryptography;
using System.Text;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Logging;
using Imazen.Abstractions.Resulting;
using Imazen.Common.Extensibility.Support;

namespace Imazen.HybridCache.MetaStore
{
    internal class MetaStore : ICacheDatabase<ICacheDatabaseRecord>
    {
        private readonly Shard[] shards;

        private readonly string physicalCacheDir;

        private static long GetDirectoryEntriesBytesTotal(HashBasedPathBuilder builder)
        {
            return builder.CalculatePotentialDirectoryEntriesFromSubfolderBitCount() *
                CleanupManager.DirectoryEntrySize() + CleanupManager.DirectoryEntrySize();
        }

        public MetaStore(MetaStoreOptions options, HybridCacheAdvancedOptions cacheOptions, IReLogger logger)
        {
            if (options.Shards <= 0 || options.Shards > 2048)
            {
                throw new ArgumentException("Shards must be between 1 and 2048");
            }

            physicalCacheDir = cacheOptions.PhysicalCacheDir;
            var pathBuilder =
                new HashBasedPathBuilder(cacheOptions.PhysicalCacheDir, cacheOptions.Subfolders, '/', ".jpg");

            var directoryEntriesBytes =
                GetDirectoryEntriesBytesTotal(pathBuilder) + CleanupManager.DirectoryEntrySize();

            var shardCount = options.Shards;
            shards = new Shard[shardCount];
            for (var i = 0; i < shardCount; i++)
            {
                shards[i] = new Shard(i, options, Path.Combine(options.DatabaseDir, "db", i.ToString()),
                    directoryEntriesBytes / shardCount, logger);
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

        public Task<DeleteRecordResult> DeleteRecord(int shard, ICacheDatabaseRecord record)
        {
            if (record.CreatedAt > DateTime.UtcNow)
                throw new InvalidOperationException();
            return shards[shard].DeleteRecord(record);
        }

        public Task<CodeResult> TestRootDirectory()
        {
            try
            {
                // Try to create the root directory if it's missing
                if (!Directory.Exists(physicalCacheDir))
                    Directory.CreateDirectory(physicalCacheDir);
                // if it exists we assume that it is readable recursively
                return Task.FromResult(CodeResult.Ok());
            }
            catch (Exception ex)
            {
                return Task.FromResult(CodeResult.FromException(ex));
            }

        }

        public Task<IEnumerable<ICacheDatabaseRecord>> GetDeletionCandidates(int shard,
            DateTime maxLastDeletionAttemptTime, DateTime maxCreatedDate, int count,
            Func<int, ushort> getUsageCount)
        {
            return shards[shard]
                .GetDeletionCandidates(maxLastDeletionAttemptTime, maxCreatedDate, count, getUsageCount);
        }

        public Task<IEnumerable<ICacheDatabaseRecord>> LinearSearchByTag(int shard, SearchableBlobTag tag)
        {
            return shards[shard].LinearSearchByTag(tag);
        }

        public Task<long> GetShardSize(int shard)
        {
            return shards[shard].GetShardSize();
        }

        public int GetShardCount()
        {
            return shards.Length;
        }

        public Task<ICacheDatabaseRecord?> GetRecord(int shard, string relativePath)
        {
            return shards[shard].GetRecord(relativePath);
        }

        public int EstimateRecordDiskSpace(CacheDatabaseRecord newRecord)
        {
            return Shard.GetLogBytesOverhead(newRecord);
        }

        public Task<bool> CreateRecordIfSpace(int shard, CacheDatabaseRecord newRecord, long diskSpaceLimit)
        {
            return shards[shard].CreateRecordIfSpace(newRecord, diskSpaceLimit);
        }

        public Task UpdateCreatedDateAtomic(int shard, string relativePath, DateTime createdDate,
            Func<CacheDatabaseRecord> createIfMissing)
        {
            return shards[shard].UpdateCreatedDateAtomic(relativePath, createdDate, createIfMissing);
        }

        public Task ReplaceRelativePathAndUpdateLastDeletion(int shard, ICacheDatabaseRecord record,
            string movedRelativePath,
            DateTime lastDeletionAttempt)
        {
            return shards[shard]
                .ReplaceRelativePathAndUpdateLastDeletion(record, movedRelativePath, lastDeletionAttempt);
        }

        public async Task<CodeResult> TestMetaStore()
        {
            try
            {
                var lastFailed = shards.FirstOrDefault(s => s.FailedToStart);
                if (lastFailed != null)
                {
                    await lastFailed.TryStart();
                }

                await Task.WhenAll(shards.Select(s => s.TryStart()));

                return CodeResult.Ok();
            }
            catch (Exception ex)
            {
                return CodeResult.FromException(ex);
            }
        }
    }
}