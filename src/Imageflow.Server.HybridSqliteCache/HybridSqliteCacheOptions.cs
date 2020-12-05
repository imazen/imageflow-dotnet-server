namespace Imageflow.Server.HybridSqliteCache
{
    public class HybridSqliteCacheOptions
    {
        public string DatabaseDir { get; set; }
        
        public string DiskCacheDir { get; set; }

        /// <summary>
        /// Defaults to 100MiB. 
        /// </summary>
        public long MaxWriteQueueBytes { get; set; } = 100 * 1024 * 1024;

        /// <summary>
        /// Defaults to 1 GiB
        /// </summary>
        public long CacheSizeLimitInBytes { get; set; } = 1 * 1024 * 1024 * 1024;
        
        /// <summary>
        /// The minimum number of bytes to free when running a cleanup task. Defaults to 1MiB;
        /// </summary>
        public long MinCleanupBytes { get; set; } = 1 * 1024 * 1024;
        
        public HybridSqliteCacheOptions(string cacheDir)
        {
            DatabaseDir = cacheDir;
            DiskCacheDir = cacheDir;
        }
    }
}