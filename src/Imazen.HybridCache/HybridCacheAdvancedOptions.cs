namespace Imazen.HybridCache
{
    public class HybridCacheAdvancedOptions
    {
        
        
        public HybridCacheAdvancedOptions(string uniqueName, string physicalCacheDir)
        {
            PhysicalCacheDir = physicalCacheDir;
            UniqueName = uniqueName;
            CleanupManagerOptions = new CleanupManagerOptions();

            AsyncCacheOptions = new AsyncCacheOptions()
            {
                UniqueName = uniqueName
            };
        }
        
        public AsyncCacheOptions AsyncCacheOptions { get; set; }
        
        public CleanupManagerOptions CleanupManagerOptions { get; set; }

        /// <summary>
        /// Controls how many subfolders to use for disk caching.
        /// Rounded to the next power of to. (1->2, 3->4, 5->8, 9->16, 17->32, 33->64, 65->128,129->256,etc.)
        /// NTFS does not handle more than 8,000 files per folder well. 
        /// Defaults to 2048
        /// </summary>
        public int Subfolders { get; set; } = 2048;

        
        /// <summary>
        /// Sets the location of the cache directory and database files.
        /// </summary>
        public string PhysicalCacheDir { get; set; }
        
        
        public string UniqueName { get; }
        
        
        /// <summary>
        /// The number of shards to create. Defaults to 8
        /// </summary>
        public int Shards { get; set; } = 8;

        /// <summary>
        /// The maximum number of log files per shard to permit. Log files will be merged when this value is exceeded.
        /// </summary>
        public int MaxLogFilesPerShard { get; set; } = 5;

        /// <summary>
        /// If true, cache files are first written to a temp file, then moved into their correct place.
        /// Slightly slower when true. Defaults to false.
        /// </summary>
        public bool MoveFilesIntoPlace { get; set; } = false;


    }
}