namespace Imazen.HybridCache
{
    public class HybridCacheOptions
    {
        
        /// <summary>
        /// A unique name for this cache so it can be referenced by configuration and routes.
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Where to store the cached files and the database
        /// </summary>
        public string DiskCacheDirectory { get; set; }

        /// <summary>
        /// How many RAM bytes to use when writing asynchronously to disk before we switch to writing synchronously.
        /// Defaults to 100MiB. 
        /// </summary>
        [Obsolete("This is ignored; use the new shared write queue configuration instead")]
        public long QueueSizeLimitInBytes { get; set; } = 100 * 1024 * 1024;

        /// <summary>
        /// Defaults to 1 GiB. Don't set below 9MB or no files will be cached, since 9MB is reserved just for empty directory entries.
        /// </summary>
        public long CacheSizeLimitInBytes { get; set; } = 1 * 1024 * 1024 * 1024;
        
        /// <summary>
        /// The minimum number of bytes to free when running a cleanup task. Defaults to 1MiB;
        /// </summary>
        public long MinCleanupBytes { get; set; } = 1 * 1024 * 1024;

        /// <summary>
        ///     How many MiB of ram to use when writing asynchronously to disk before we switch to writing synchronously.
        ///     Defaults to 100MiB.
        /// </summary>
        [Obsolete("This is ignored; use the new shared write queue configuration instead")]
        public long WriteQueueMemoryMb
        {
            get => QueueSizeLimitInBytes / 1024 / 1024;
            set => QueueSizeLimitInBytes = value * 1024 * 1024;
        }

        /// <summary>
        ///     Defaults to 1 GiB. Don't set below 9MB or no files will be cached, since 9MB is reserved just for empty directory
        ///     entries.
        /// </summary>
        public long CacheSizeMb { 
            get => CacheSizeLimitInBytes / 1024 / 1024;
            set => CacheSizeLimitInBytes = value * 1024 * 1024;
        }

        /// <summary>
        ///     The minimum number of mibibytes (1024*1024) to free when running a cleanup task. Defaults to 1MiB;
        /// </summary>
        public long EvictionSweepSizeMb { 
            get => MinCleanupBytes / 1024 / 1024;
            set => MinCleanupBytes = value * 1024 * 1024;
        }

        /// <summary>
        /// The minimum age of files to delete. Defaults to 10 seconds.
        /// </summary>
        public TimeSpan MinAgeToDelete { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The number of shards to split the metabase into. More shards means more open log files, slower shutdown.
        /// But more shards also mean less lock contention and faster start time for individual cached requests.
        /// Defaults to 8. You have to delete the database directory each time you change this number.
        /// TODO: hash incompatible settings into a root folder inside the diskcache dir
        /// </summary>
        public int DatabaseShards { get; set; } = 8;
        
        public HybridCacheOptions(string cacheDir)
        {
            DiskCacheDirectory = cacheDir;
            UniqueName = "disk";
        }

        public HybridCacheOptions(string uniqueName, string cacheDir)
        {
            UniqueName = uniqueName;
            DiskCacheDirectory = cacheDir;
        }
    }
}