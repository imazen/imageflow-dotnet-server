using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Imazen.HybridCache
{
    internal class CleanupManager : ICacheCleanupManager
    {
        private Lazy<BucketCounter> accessCounter;

        /// <summary>
        /// Creation of AccessCounter is not synchronized because we don't care if distinct references are handed out
        /// </summary>
        private BucketCounter AccessCounter => accessCounter.Value;

      
        private ICacheDatabase Database { get; }
        private CleanupManagerOptions Options { get; }
        
        private HashBasedPathBuilder PathBuilder { get; }
        private ILogger Logger { get; }
        public CleanupManager(CleanupManagerOptions options, ICacheDatabase database, ILogger logger, HashBasedPathBuilder pathBuilder)
        {
            PathBuilder = pathBuilder;
            Logger = logger;
            Options = options;
            Database = database;
            accessCounter = new Lazy<BucketCounter>(() => new BucketCounter(Options.AccessTrackingBits));
        }
        public void NotifyUsed(CacheEntry cacheEntry)
        {
            AccessCounter.Increment(cacheEntry.Hash);
        }

        public Task<string> GetContentType(CacheEntry cacheEntry, CancellationToken cancellationToken)
        {
            return Database.GetContentType(cacheEntry.RelativePath);
        }

        private int EstimateEntryBytesWithOverhead(int byteCount)
        {
            // Most file systems have a 4KiB block size
            var withBlockSize = (Math.Max(1, byteCount) + 4095) / 4096 * 4096;
            // Add 1KiB for the file descriptor and 1KiB for the directory descriptor, although it's probably larger
            var withFileDescriptor = withBlockSize + 1024 + 1024;

            return withFileDescriptor;
        }

        private async Task<bool> EvictSpace(long diskSpace, CancellationToken cancellationToken)
        {
            
            var bytesToDeleteOptimally = Math.Max(Options.MinCleanupBytes, diskSpace);
            var bytesToDeleteMin = diskSpace;

            long bytesDeleted = 0;

            while (bytesDeleted < bytesToDeleteOptimally)
            {
                var deletionCutoff = DateTime.UtcNow.Subtract(Options.RetryDeletionAfter);
                var creationCutoff = DateTime.UtcNow.Subtract(Options.MinAgeToDelete);
                
                var records =
                    (await Database.GetOldestRecords(deletionCutoff, creationCutoff, Options.CleanupSelectBatchSize))
                    .Select(r =>
                        new Tuple<ushort, ICacheDatabaseRecord>(
                            AccessCounter.Get(r.AccessCountKey), r))
                    .OrderByDescending(r => r.Item1)
                    .Select(r => r.Item2).ToArray();
                
                foreach (var record in records)
                {
                    if (bytesDeleted >= bytesToDeleteOptimally) break;

                    var deletedBytes = await TryDeleteRecord(record);
                    bytesDeleted += deletedBytes;
                }

                // Unlikely to find more records in the next iteration
                if (records.Length < Options.CleanupSelectBatchSize)
                {
                    // If we hit the bare minimum, return OK 
                    return bytesDeleted < bytesToDeleteMin;
                }
            }
            return true; 
        }

        public async Task<bool> TryReserveSpace(CacheEntry cacheEntry, string contentType, int byteCount,
            bool allowEviction,
            CancellationToken cancellationToken)
        {
            var entryDiskSpace = EstimateEntryBytesWithOverhead(byteCount) +
                            Database.EstimateRecordDiskSpace(cacheEntry.RelativePath.Length);

            var farFuture = DateTime.UtcNow.AddYears(100);

            for (var attempts = 0; attempts < 3; attempts++)
            {
                var recordCreated =
                    await Database.CreateRecordIfSpace(cacheEntry.RelativePath,
                        contentType,
                        entryDiskSpace,
                        farFuture,
                        AccessCounter.GetHash(cacheEntry.Hash),
                        Options.MaxCacheBytes);

                // Return true if we created the record
                if (recordCreated) return true;

                // We need to evict but we are not permitted
                if (!allowEviction) return false;

                var missingSpace = Math.Max(0, await Database.GetTotalBytes() + entryDiskSpace - Options.MaxCacheBytes);
                // Evict space 
                if (!await EvictSpace(missingSpace, cancellationToken))
                {
                    return false; //We failed to evict enough space from the cache
                }
            }

            return false;
        }

        public Task MarkFileCreated(CacheEntry cacheEntry)
        {
            return Database.UpdateCreatedDate(cacheEntry.RelativePath, DateTime.UtcNow);
        }

        private async Task<long> TryDeleteRecord(ICacheDatabaseRecord record)
        {
            var physicalPath = PathBuilder.GetPhysicalPathFromRelativePath(record.RelativePath);
            try
            {
                File.Delete(physicalPath);
                if (await Database.DeleteRecord(record.RelativePath))
                {
                    return record.DiskSize;
                }
                return 0;
            }
            catch (FileNotFoundException)
            {
                if (await Database.DeleteRecord(record.RelativePath))
                {
                    return record.DiskSize;
                }
                return 0;
            }
            catch (IOException)
            {
                if (physicalPath.Contains(".moving_"))
                {
                    // We already moved it. All we can do is update the last deletion attempt
                    await Database.UpdateLastDeletionAttempt(record.RelativePath, DateTime.Now);
                    return 0;
                }
                var movedRelativePath = record.RelativePath + ".moving_" +
                                        new Random().Next(int.MaxValue).ToString("x", CultureInfo.InvariantCulture);
                var movedPath = PathBuilder.GetPhysicalPathFromRelativePath(movedRelativePath);
                try
                {
                    //Move it so it usage will decrease and it can be deleted later
                    File.Move(physicalPath, movedPath);
                    await Database.ReplaceRelativePathAndUpdateLastDeletion(record, movedRelativePath,
                        DateTime.Now);
                    return 0;
                }
                catch (IOException)
                {
                    await Database.UpdateLastDeletionAttempt(record.RelativePath, DateTime.Now);
                    return 0;
                }
            }
        }

    }
}