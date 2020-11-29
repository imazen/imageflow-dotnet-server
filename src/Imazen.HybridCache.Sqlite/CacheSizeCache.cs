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
        private long cacheSize;

        private TimeSpan cacheExpiresAfter = TimeSpan.FromMilliseconds(1500);
        public CacheSizeCache(Func<long> getCacheSize)
        {
            this.getCacheSize = getCacheSize;
        }
        
        public long GetTotalBytes()
        {
            var now = Stopwatch.GetTimestamp();
            if (now > lastFetchedCacheSize + cacheExpiresAfter.Ticks)
            {
                cacheSize = getCacheSize();
                lastFetchedCacheSize = now;
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