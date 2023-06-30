using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.HybridCache.MetaStore;
using Xunit;

namespace Imazen.HybridCache.Tests
{
    public class HybridCacheTests
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

                Task<IStreamCacheInput> DataProvider(CancellationToken token)
                {
                    return Task.FromResult(new StreamCacheInput(contentType, new ArraySegment<byte>(new byte[4000])).ToIStreamCacheInput());
                }

                var result = await cache.GetOrCreateBytes(key, DataProvider, cancellationToken, true);
                Assert.Equal("WriteSucceeded", result.Status);
                await result.Data.DisposeAsync();
                
                var result2 = await cache.GetOrCreateBytes(key, DataProvider, cancellationToken, true);
                Assert.Equal("DiskHit", result2.Status);
                Assert.Equal(contentType, result2.ContentType);
                Assert.NotNull(result2.Data);
                
                Assert.NotNull(((AsyncCache.AsyncCacheResult)result2).CreatedAt);
                await result2.Data.DisposeAsync();
                await cache.AsyncCache.AwaitEnqueuedTasks();
                
                var result3 = await cache.GetOrCreateBytes(key, DataProvider, cancellationToken, true);
                Assert.Equal("DiskHit", result3.Status);
                Assert.Equal(contentType, result3.ContentType);
                Assert.NotNull(((AsyncCache.AsyncCacheResult)result3).CreatedAt);
                Assert.NotNull(result3.Data);
                await result3.Data.DisposeAsync();
                var key2 = new byte[] {2, 1, 2, 3};
                Task<IStreamCacheInput> DataProvider2(CancellationToken token)
                {
                    return Task.FromResult(new StreamCacheInput(null, new ArraySegment<byte>(new byte[4000])).ToIStreamCacheInput());
                }
                var result4 = await cache.GetOrCreateBytes(key2, DataProvider2, cancellationToken, true);
                Assert.Equal("WriteSucceeded", result4.Status);
                await result4.Data.DisposeAsync();
                var result5 = await cache.GetOrCreateBytes(key2, DataProvider, cancellationToken, true);
                Assert.Equal("DiskHit", result5.Status);
                Assert.Null(result5.ContentType);
                Assert.NotNull(result5.Data);
                await result5.Data.DisposeAsync();
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