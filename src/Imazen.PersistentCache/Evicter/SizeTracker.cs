using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.PersistentCache.Evicter
{
    class SizeTracker
    {
        private readonly uint shardId;
        private readonly IPersistentStore store;
        private readonly PersistentCacheSettings settings;
        private ulong? lastCount;
        private DateTimeOffset lastTimeRead = DateTimeOffset.MinValue;
        private readonly IClock clock;
        private readonly AsyncLock sizeLock = new AsyncLock();
        public SizeTracker(uint shardId, IPersistentStore store, IClock clock, PersistentCacheSettings settings)
        {
            this.shardId = shardId;
            this.store = store;
            this.settings = settings;
            this.clock = clock;
        }

        private string KeyName => "cache-size";

        async Task<ulong> ReadSize()
        {
            using (var readStream = await store.ReadStream(shardId, KeyName, CancellationToken.None))
            {
                if (readStream != null)
                {
                    var buffer = new byte[8];
                    await readStream.ReadAsync(buffer, 0, buffer.Length);
                    return BitConverter.ToUInt64(buffer, 0);

                }
                else
                {
                    return 0;
                }
            }
        }
        internal async Task OffsetBy(long byteCount)
        {
            using (var mutex = await sizeLock.LockAsync())
            {
                var currentCount = await ReadSize();
                var newCount = (ulong)Math.Max(0, (long)currentCount + byteCount);
                lastCount = newCount;
                lastTimeRead = clock.GetUtcNow();
                await store.WriteBytes(shardId, KeyName, BitConverter.GetBytes(newCount), CancellationToken.None);
            }
        }

        internal async Task<ulong> GetCachedSize() {
            if (!lastCount.HasValue ||  lastTimeRead.AddMinutes(5) < clock.GetUtcNow())
            {
                lastCount = await ReadSize();
                lastTimeRead = clock.GetUtcNow();
            }
            return lastCount.Value;
        }

    }
}
