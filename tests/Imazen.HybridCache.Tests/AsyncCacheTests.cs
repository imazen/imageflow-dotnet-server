using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.StreamCache;
using Xunit;

namespace Imazen.HybridCache.Tests
{
    public class AsyncCacheTests
    {
        internal class NullCacheManager : ICacheCleanupManager
        {
            public void NotifyUsed(CacheEntry cacheEntry){}
            public Task<string> GetContentType(CacheEntry cacheEntry, CancellationToken cancellationToken) => null;
            public Task<bool> TryReserveSpace(CacheEntry cacheEntry, string contentType, int byteCount, bool allowEviction,
                CancellationToken cancellationToken) => Task.FromResult(true);

            public Task MarkFileCreated(CacheEntry cacheEntry)
            {
                return Task.FromResult(true);
            }
        }

        [Fact]
        public async void SmokeTestAsyncMissHit()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");
            Directory.CreateDirectory(path);
            AsyncCache cache = null;
            try
            {
                var builder = new HashBasedPathBuilder(path,
                    8192, '/', ".jpg");
                
                cache = new AsyncCache(new AsyncCacheOptions(), new NullCacheManager(), builder,null);

                var keyBasis = new byte[] {6,1,2};
                var result = await cache.GetOrCreateBytes(keyBasis, (token) =>
                    {
                        return Task.FromResult(new Tuple<string, ArraySegment<byte>>(
                            null, new ArraySegment<byte>(new byte[] {3, 2, 1})));
                    },
                    CancellationToken.None, false);
                Assert.NotNull(result.Data);
                Assert.Equal(StreamCacheQueryResult.Miss, result.Result);

                await cache.AwaitEnqueuedTasks();

                var result2 = await cache.GetOrCreateBytes(keyBasis, (token) =>
                    {
                        return Task.FromResult(new Tuple<string, ArraySegment<byte>>(
                            null, new ArraySegment<byte>(new byte[] {3, 2, 1})));
                    },
                    CancellationToken.None, false);
                Assert.NotNull(result2.Data);
                Assert.Equal(StreamCacheQueryResult.Hit, result2.Result);

                
                var hash = builder.HashKeyBasis(keyBasis);
                var expectedPhysicalPath = builder.GetPhysicalPathFromHash(hash);
                Assert.True(File.Exists(expectedPhysicalPath));
            }
            finally
            {
                try
                {
                    cache?.AwaitEnqueuedTasks();
                }
                finally
                {
                    Directory.Delete(path, true);
                }
            }
        }
        
        
        [Fact]
        public async void SmokeTestSyncMissHit()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");
            Directory.CreateDirectory(path);
            AsyncCache cache= null;
            try
            {
                var asyncCacheOptions = new AsyncCacheOptions()
                {
                    MaxQueuedBytes = 0
                };
                var builder = new HashBasedPathBuilder(path,
                    8192, '/', ".jpg");
                
                cache = new AsyncCache(asyncCacheOptions, new NullCacheManager(), builder,null);

                var keyBasis = new byte[] {6,1,2};
                var result = await cache.GetOrCreateBytes(keyBasis, (token) =>
                    {
                        return Task.FromResult(new Tuple<string, ArraySegment<byte>>(
                            null, new ArraySegment<byte>(new byte[] {3, 2, 1})));
                    },
                    CancellationToken.None, false);
                Assert.NotNull(result.Data);
                Assert.Equal(StreamCacheQueryResult.Miss, result.Result);

                await cache.AwaitEnqueuedTasks();

                var result2 = await cache.GetOrCreateBytes(keyBasis, (token) =>
                    {
                        return Task.FromResult(new Tuple<string, ArraySegment<byte>>(
                            null, new ArraySegment<byte>(new byte[] {3, 2, 1})));
                    },
                    CancellationToken.None, false);
                Assert.NotNull(result2.Data);
                Assert.Equal(StreamCacheQueryResult.Hit, result2.Result);
                var hash = builder.HashKeyBasis(keyBasis);
                var expectedPhysicalPath = builder.GetPhysicalPathFromHash(hash);
                Assert.True(File.Exists(expectedPhysicalPath));
            }
            finally
            {
                try
                {
                    await cache?.AwaitEnqueuedTasks();
                }
                finally
                {
                    Directory.Delete(path, true);
                }
            }
        }

    }
}