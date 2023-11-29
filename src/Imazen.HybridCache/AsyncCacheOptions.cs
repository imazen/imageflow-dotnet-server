namespace Imazen.HybridCache
{
    
    public class AsyncCacheOptions
    {
        
        
        public int WaitForIdenticalDiskWritesMs { get; set;  } = 15000;
        
        public int WaitForIdenticalCachePutTimeoutMs { get; set; } = 100000;

        
        [Obsolete("Ignored; write queue is now global across all caches")]
        public long MaxQueuedBytes { get; set;  } = 1024 * 1024 * 100;

        [Obsolete("Ignored; write queue is now global across all caches")]
        public bool FailRequestsOnEnqueueLockTimeout { get; set; } = true;
        
        [Obsolete("Ignored; write queue is now global across all caches")]
        public bool WriteSynchronouslyWhenQueueFull { get; set; } = false;
        
        /// <summary>
        /// If you're not on .NET 6/8, but you have  File.Move(from, to, true) available,
        /// you can set this to true to use that instead of the default File.Move(from,to) for better performance
        /// Used by deletion code even if MoveFilesIntoPlace is false
        /// </summary>
        public Action<string,string>? MoveFileOverwriteFunc { get; set; }

        /// <summary>
        /// If true, cache files are first written to a temp file, then moved into their correct place.
        /// Slightly slower when true. Defaults to false.
        /// </summary>
        public bool MoveFilesIntoPlace { get; set; }

        public required string UniqueName { get; set; }
    }
}