using System;

namespace Imageflow.Server.HybridCache
{
    public class HybridCacheOptions
    {
        public string DiskCacheDirectory { get; set; }

        /// <summary>
        /// Defaults to 100MiB. 
        /// </summary>
        public long MaxWriteQueueBytes { get; set; } = 100 * 1024 * 1024;

        /// <summary>
        /// Defaults to 1 GiB. Don't set below 35MB or no files will be cached
        /// </summary>
        public long CacheSizeLimitInBytes { get; set; } = 1 * 1024 * 1024 * 1024;
        
        /// <summary>
        /// The minimum number of bytes to free when running a cleanup task. Defaults to 1MiB;
        /// </summary>
        public long MinCleanupBytes { get; set; } = 1 * 1024 * 1024;
        
        /// <summary>
        /// The minimum age of files to delete.
        /// </summary>
        public TimeSpan MinAgeToDelete { get; set; } = TimeSpan.FromSeconds(60);

        public HybridCacheOptions(string cacheDir)
        {
            DiskCacheDirectory = cacheDir;
        }
    }
}