using System;

namespace Imazen.DiskCache
{
    public class ClassicDiskCacheSettings
    {
        public ClassicDiskCacheSettings(){}


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
                throw new InvalidOperationException("You cannot change settings after the DiskCache has started");
        }
        private int subfolders = 8192;
        /// <summary>
        /// Controls how many subfolders to use for disk caching. Rounded to the next power of to. (1->2, 3->4, 5->8, 9->16, 17->32, 33->64, 65->128,129->256,etc.)
        /// NTFS does not handle more than 8,000 files per folder well. Larger folders also make cleanup more resource-intensive.
        /// Defaults to 8192, which combined with the default setting of 400 images per folder, allows for scalability to ~1.5 million actively used image versions. 
        /// For example, given a desired cache size of 100,000 items, this should be set to 256.
        /// </summary>
        public int Subfolders {
            get => subfolders;
            set { BeforeSettingChanged(); subfolders = value; }
        }

        private bool enabled = true;
        /// <summary>
        /// Allows disk caching to be disabled for debugging purposes. Defaults to true.
        /// </summary>
        public bool Enabled {
            get => enabled;
            set { BeforeSettingChanged(); enabled = value; }
        }


        private bool autoClean = false;
        /// <summary>
        /// If true, items from the cache folder will be automatically 'garbage collected' if the cache size limits are exceeded.
        /// Defaults to false.
        /// </summary>
        public bool AutoClean {
            get => autoClean;
            set { BeforeSettingChanged();  autoClean = value; }
        }
        private CleanupStrategy cleanupStrategy = new CleanupStrategy();
        /// <summary>
        /// Only relevant when AutoClean=true. Settings about how background cache cleanup are performed.
        /// It is best not to modify these settings. There are very complicated and non-obvious factors involved in their choice.
        /// </summary>
        public CleanupStrategy CleanupStrategy {
            get => cleanupStrategy;
            set
            {
                BeforeSettingChanged();
                cleanupStrategy = value ?? throw new ArgumentNullException(nameof(value), "value cannot be null");
            }
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
                //Default to application-relative path if no leading slash is present. 
                //Resolve the tilde if present.
                physicalCacheDir =  string.IsNullOrEmpty(value) ? null : value;
            }
        }

    }
}