using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.HybridCache.Sqlite
{
    public class CacheSizeCache
    {
        private Func<Task<long>> getCacheSize;

        private long lastFetchedCacheSize = 0;
        private long cacheSize = 0;

        private TimeSpan cacheExpiresAfter = TimeSpan.FromMilliseconds(1500);
        public CacheSizeCache(Func<Task<long>> getCacheSize)
        {
            this.getCacheSize = getCacheSize;
        }
        
        public async Task<long> GetTotalBytes()
        {
            var now = Stopwatch.GetTimestamp();
            if (now > lastFetchedCacheSize + cacheExpiresAfter.Ticks)
            {
                cacheSize = await getCacheSize();
                lastFetchedCacheSize = now;
            }
            return cacheSize;
        }
        
        public async Task BeforeDeleted(long bytes)
        {
            var unused = await GetTotalBytes();
            Interlocked.Add(ref cacheSize, bytes);
        }

        public async Task BeforeAdded(long bytes)
        {
            var unused = await GetTotalBytes();
            Interlocked.Add(ref cacheSize, -bytes);
        }
    }
}