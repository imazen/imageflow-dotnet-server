using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.HybridCache.MetaStore;
using Xunit;

namespace Imazen.HybridCache.Tests
{
    public class HybridCacheSqliteTests
    {
        [Fact]
        public async void SmokeTest()
        {
            var cancellationToken = CancellationToken.None;
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");
            Directory.CreateDirectory(path);
            var cacheOptions = new HybridCacheOptions(path)
            {
                AsyncCacheOptions = new AsyncCacheOptions()
                {
                    MaxQueuedBytes = 0
                }
            };
            var database = new MetaStore.MetaStore(new MetaStoreOptions(path), cacheOptions, null);
            HybridCache cache = new HybridCache(database,cacheOptions, null);
            try
            {
                await cache.StartAsync(cancellationToken);

                var key = new byte[] {0, 1, 2, 3};
                var contentType = "application/octet-stream";

                Task<Tuple<string, ArraySegment<byte>>> DataProvider(CancellationToken token)
                {
                    return Task.FromResult(new Tuple<string, ArraySegment<byte>>(contentType, new ArraySegment<byte>(new byte[4000])));
                }

                var result = await cache.GetOrCreateBytes(key, DataProvider, cancellationToken, true);
                
                Assert.Equal("WriteSucceeded", result.Status);
                
                var result2 = await cache.GetOrCreateBytes(key, DataProvider, cancellationToken, true);
                Assert.Equal("DiskHit", result2.Status);
                Assert.Equal(contentType, result2.ContentType);
                Assert.NotNull(result2.Data);
                
                await cache.AsyncCache.AwaitEnqueuedTasks();
                
                var result3 = await cache.GetOrCreateBytes(key, DataProvider, cancellationToken, true);
                Assert.Equal("DiskHit", result3.Status);
                Assert.Equal(contentType, result3.ContentType);
                Assert.NotNull(result3.Data);


            }
            finally
            {
                try
                {
                    await cache.StopAsync(cancellationToken);
                }
                finally
                {
                    Directory.Delete(path, true);
                }
            }

        }
    }
}