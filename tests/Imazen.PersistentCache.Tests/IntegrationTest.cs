using System;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Imazen.PersistentCache.Tests
{
    public class IntegrationTest
    {
        [Fact(Skip = "Too much disk churn")]
        public async void Test1()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");
            Directory.CreateDirectory(path);
            var store = new PersistentDiskStore(path);
            var settings = new PersistentCacheOptions()
            {
                FreeSpacePercentGoal = 50,
                MaxCachedBytes = 1000,
                MaxWriteLogSize = 800,
                WriteLogFlushIntervalMs = 1,
                ReadInfoFlushIntervalMs = 1,

                ShardCount = 1,
            };
            var clock = new CacheClock(); //new FakeClock("2020-05-25");
            var cache = new PersistentCache(store, clock, settings);
            try
            {
                await cache.StartAsync(CancellationToken.None);

                
                var dataBytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

                for (int i = 0; i < 10; i++)
                {
                    var key = new CacheKey
                    {
                        Key1 = Encoding.UTF8.GetBytes($"file{i}.jpg"),
                        Key2 = Encoding.UTF8.GetBytes("2020-05-25 00:00:00"),
                        Key3 = Encoding.UTF8.GetBytes("?width=200"),
                    };

                    cache.PutBytesEventually(key, dataBytes, 1);
                }

                await cache.FlushWrites();
                //Thread.Sleep(50);
                //clock.AdvanceSeconds(45);
                Thread.Sleep(2000);
                await cache.FlushWrites();


                for (int i = 0; i < 10; i++)
                {
                    var key = new CacheKey
                    {
                        Key1 = Encoding.UTF8.GetBytes($"file{i}.jpg"),
                        Key2 = Encoding.UTF8.GetBytes("2020-05-25 00:00:00"),
                        Key3 = Encoding.UTF8.GetBytes("?width=200"),
                    };

                    using (var stream = await cache.GetStream(key, CancellationToken.None))
                    {
                        Assert.Equal(0, stream.ReadByte());
                        Assert.Equal(1, stream.ReadByte());
                    }
                }


                var exceptions = cache.PopExceptions();
                foreach (var e in exceptions)
                {
                    throw e;
                }




                await cache.StopAsync(CancellationToken.None);
            }
            finally
            {
                try
                {
                    await cache.StopAsync(CancellationToken.None);
                }
                finally
                {
                    Directory.Delete(path, true);
                }
            }
        }
    }
}
