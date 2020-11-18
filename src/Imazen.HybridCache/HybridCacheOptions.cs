using System;

namespace Imazen.HybridCache
{
    public class HybridCacheOptions
    {
        /// <summary>
        /// The maximum number of bytes to store on disk
        /// </summary>
        public long MaxCacheSizeInBytes { get; set; } = 2 * 1024 * 1024;
        
        public HybridCacheOptions(string physicalCacheDir)
        {
            this.PhysicalCacheDir = physicalCacheDir;
        }


        private bool immutable = false;
        internal void MakeImmutable()
        {
            if (PhysicalCacheDir == null) throw new ArgumentNullException($"PhysicalCacheDir", "PhysicalCacheDir must be set to a non-null value");
            immutable = true; 
            //TODO: make cleanupStrategy.MakeImmutable()
        }

        private void BeforeSettingChanged()
        {
            if (immutable)
                throw new InvalidOperationException("You cannot change settings after the HybridCache has started");
        }
        private int subfolders = 8192;
        /// <summary>
        /// Controls how many subfolders to use for disk caching.
        /// Rounded to the next power of to. (1->2, 3->4, 5->8, 9->16, 17->32, 33->64, 65->128,129->256,etc.)
        /// NTFS does not handle more than 8,000 files per folder well. 
        /// Defaults to 8192
        /// </summary>
        public int Subfolders {
            get => subfolders;
            set { BeforeSettingChanged(); subfolders = value; }
        }

      

        /// <summary>
        /// Sets the timeout time to 15 seconds as default.
        /// </summary>
        private int cacheAccessTimeout = 15000;
        /// <summary>
        /// How many milliseconds to wait for a cached item to be available. Values below 0 are set to 0. Defaults to 15 seconds.
        /// Actual time spent waiting may be 2 or 3x this value, if multiple layers of synchronization require a wait.
        /// </summary>
        public int CacheAccessTimeout {
            get => cacheAccessTimeout;
            set { BeforeSettingChanged(); cacheAccessTimeout = Math.Max(value,0); }
        }


        private bool asyncWrites = true;
        /// <summary>
        /// If true, writes to the disk cache will be performed outside the request thread, allowing responses to return to the client quicker. 
        /// </summary>
        public bool AsyncWrites {
            get => asyncWrites;
            set { BeforeSettingChanged(); asyncWrites = value; }
        }


        private int asyncBufferSize = 1024 * 1024 * 10;
        /// <summary>
        /// If more than this amount of memory (in bytes) is currently allocated by queued writes, the request will be processed synchronously instead of asynchronously.
        /// </summary>
        public int AsyncBufferSize {
            get => asyncBufferSize;
            set { BeforeSettingChanged(); asyncBufferSize = value; }
        }


        private string physicalCacheDir =  null;
        /// <summary>
        /// Sets the location of the cache directory. 
        /// </summary>
        public string PhysicalCacheDir { 
            get => physicalCacheDir;
            set {
                BeforeSettingChanged();
                physicalCacheDir =  string.IsNullOrEmpty(value) ? null : value;
            }
        }
        
        private string databaseDir =  null;
        /// <summary>
        /// Sets the location of the database directory. Can be same as cache directory, or on a faster drive.
        /// </summary>
        public string DatabaseDir { 
            get => databaseDir;
            set {
                BeforeSettingChanged();
                databaseDir =  string.IsNullOrEmpty(value) ? null : value;
            }
        }
        
    }
}