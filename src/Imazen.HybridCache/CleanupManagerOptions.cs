using System;

namespace Imazen.HybridCache
{
    public class CleanupManagerOptions
    {
        /// <summary>
        /// Suggested values are 21, which uses 4.1MB of memory.
        /// Increase up to 31 to improve granularity
        /// </summary>
        public int AccessTrackingBits { get; set; } = 21;

        /// <summary>
        /// The minimum time to wait before trying to delete a file again if file deletion fails
        /// </summary>
        public TimeSpan RetryDeletionAfter { get; set; } = TimeSpan.FromMinutes(20);

        /// <summary>
        /// Defaults to 1 gigabyte. The maximum number of bytes permitted to be stored.
        /// </summary>
        public long MaxCacheBytes { get; set; } = 1024 * 1024 * 1024;

        /// <summary>
        /// The minimum number of bytes to free when running a cleanup task. Defaults to 1MiB;
        /// </summary>
        public long MinCleanupBytes { get; set; } = 1 * 1024 * 1024;

        /// <summary>
        /// The minimum age of files to delete. Must be long enough for new files to be written to disk after their
        /// records are created, or orphaned files can be created.
        /// </summary>
        public TimeSpan MinAgeToDelete { get; set; } = TimeSpan.FromSeconds(60);
        
        /// <summary>
        /// How many records to pull at a time when doing cleanup
        /// </summary>
        public int CleanupSelectBatchSize { get; set; } = 100;
    }
}