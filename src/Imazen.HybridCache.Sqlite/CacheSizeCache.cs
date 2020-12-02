using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.HybridCache.Sqlite
{
    /// <summary>
    /// This class uses Interlocked.Add to track changes, and refreshes the total size from the DB every 1500ms.
    /// We tried using an AsyncLock but it drastically slowed things down.
    /// </summary>
    internal class CacheSizeCache
    {
        private readonly Func<long> getCacheSize;

        private long lastFetchedCacheSize;
        private long cacheSize = -1;

        private TimeSpan cacheExpiresAfter = TimeSpan.FromMilliseconds(15000);
        public CacheSizeCache(Func<long> getCacheSize)
        {
            this.getCacheSize = getCacheSize;
        }
        
        public long GetTotalBytes()
        {
            var now = Stopwatch.GetTimestamp();
            if (cacheSize == -1 || now > lastFetchedCacheSize + (long)(cacheExpiresAfter.TotalMilliseconds * Stopwatch.Frequency / 1000))
            {
                lastFetchedCacheSize = now; // Serve outdated values while we perform the update
                cacheSize = getCacheSize();
            }
            return cacheSize;
        }
        
        public void BeforeDeleted(long bytes)
        {
            var unused = GetTotalBytes();
            Interlocked.Add(ref cacheSize, bytes);
        }

        public void BeforeAdded(long bytes)
        {
            var unused =  GetTotalBytes();
            Interlocked.Add(ref cacheSize, -bytes);
        }
    }
}