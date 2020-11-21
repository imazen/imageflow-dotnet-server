namespace Imageflow.Server.HybridSqliteCache
{
    public class HybridSqliteCacheOptions
    {
        public string DatabaseDir { get; set; }
        
        public string DiskCacheDir { get; set; }
        
        public long MaxWriteQueueBytes { get; set; }
        
        public HybridSqliteCacheOptions(string cacheDir)
        {
            DatabaseDir = cacheDir;
            DiskCacheDir = cacheDir;
        }
    }
}