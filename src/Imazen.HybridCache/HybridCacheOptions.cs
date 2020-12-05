namespace Imazen.HybridCache
{
    public class HybridCacheOptions
    {
        
        
        public HybridCacheOptions(string physicalCacheDir)
        {
            PhysicalCacheDir = physicalCacheDir;
            AsyncCacheOptions = new AsyncCacheOptions();
            CleanupManagerOptions = new CleanupManagerOptions();
        }
        
        public AsyncCacheOptions AsyncCacheOptions { get; set; }
        
        public CleanupManagerOptions CleanupManagerOptions { get; set; }

        /// <summary>
        /// Controls how many subfolders to use for disk caching.
        /// Rounded to the next power of to. (1->2, 3->4, 5->8, 9->16, 17->32, 33->64, 65->128,129->256,etc.)
        /// NTFS does not handle more than 8,000 files per folder well. 
        /// Defaults to 8192
        /// </summary>
        public int Subfolders { get; set; } = 8192;

        
        /// <summary>
        /// Sets the location of the cache directory. 
        /// </summary>
        public string PhysicalCacheDir { get; set; }
        
        
    }
}