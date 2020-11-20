namespace Imazen.HybridCache
{
    public class AsyncCacheOptions
    {
        public int WaitForIdenticalRequestsTimeoutMs { get; } = 15000;
        public int WaitForIdenticalDiskWritesMs { get; } = 15000;
        public long MaxQueuedBytes { get; } = 1024 * 1024 * 100;
        public string PhysicalCachePath { get; }

        /// <summary>
        /// Rounded up to the next power of 2
        /// </summary>
        public int CacheSubfolders { get; } = 8192;

        public bool FailRequestsOnEnqueueLockTimeout { get; } = true;
        
        public bool WriteSynchronouslyWhenQueueFull { get; } = true;
        
    }
}