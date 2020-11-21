using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.HybridCache.Sqlite;
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
            var database = new SqliteCacheDatabase(new SqliteCacheDatabaseOptions(path), null);
            HybridCache cache = new HybridCache(database, new HybridCacheOptions(path), null);
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
                
                Assert.Equal(StreamCacheQueryResult.Miss, result.Result);
                
                var result2 = await cache.GetOrCreateBytes(key, DataProvider, cancellationToken, true);
                Assert.Equal(StreamCacheQueryResult.Hit, result2.Result);
                Assert.Equal(contentType, result.ContentType);
                Assert.NotNull(result.Data);
                
                await cache.AsyncCache.AwaitEnqueuedTasks();

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