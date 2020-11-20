namespace Imazen.HybridCache
{
    public class AsyncCacheOptions
    {
        public int WaitForIdenticalRequestsTimeoutMs { get; set; } = 15000;
        public int WaitForIdenticalDiskWritesMs { get; set;  } = 15000;
        public long MaxQueuedBytes { get; set;  } = 1024 * 1024 * 100;
        public string PhysicalCachePath { get; set;  }

        /// <summary>
        /// Rounded up to the next power of 2
        /// </summary>
        public int CacheSubfolders { get; set; } = 8192;

        public bool FailRequestsOnEnqueueLockTimeout { get; set; } = true;
        
        public bool WriteSynchronouslyWhenQueueFull { get; set; } = true;
        
    }
}