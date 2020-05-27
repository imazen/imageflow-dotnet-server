using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.PersistentCache
{
    internal static class ClockExtensions
    {
        public static uint GetMinutes(this IClock clock)
        {
            return (uint)(clock.GetTicks() / clock.TicksPerSecond / 60);
        }
    }
    internal class UsageTracker
    {
        struct UsageRecord
        {
            internal uint lastRead;
            internal uint frequency;
            internal UsageRecord(IClock clock)
            {
                lastRead = clock.GetMinutes();
                frequency = 1;
            }
            internal uint GetCurrentFrequency(uint nowMinutes, uint halfLifeMinutes)
            {
                if (nowMinutes - lastRead > halfLifeMinutes)
                {
                    long decayTimes = (nowMinutes - lastRead) / halfLifeMinutes;
                    return (uint)(frequency / (2 * decayTimes));
                }
                else
                {
                    return frequency;
                }
            }
        }

        long pingCount = 0;
        readonly uint halfLifeMinutes;
        readonly IClock clock;
        readonly ConcurrentDictionary<uint, UsageRecord> usage = new ConcurrentDictionary<uint, UsageRecord>();

        /// <summary>
        /// Creates a usage tracker
        /// </summary>
        /// <param name="clock">The source of time info</param>
        /// <param name="halfLifeMinutes">How often (int minutes) to halve the usage counter</param>
        internal UsageTracker(IClock clock, uint halfLifeMinutes)
        {
            this.halfLifeMinutes = halfLifeMinutes;
            this.clock = clock;
        }

        private UsageRecord PingUpdate(uint key, UsageRecord oldValue)
        {
            var now = clock.GetMinutes();
            // Saturating addition
            var oldFrequency = oldValue.GetCurrentFrequency(now, halfLifeMinutes);
            oldValue.frequency = Math.Max(oldFrequency + 1, oldFrequency);

            oldValue.lastRead = now;
            return oldValue;
        }
        internal void Ping(uint key)
        {
            usage.AddOrUpdate(key, new UsageRecord(clock), PingUpdate);
            Interlocked.Increment(ref this.pingCount);
        }

        internal long PingCount()
        {
            return this.pingCount;
        }

        internal uint GetFrequency(uint key)
        {
            if (usage.TryGetValue(key, out UsageRecord r))
            {
                return r.GetCurrentFrequency(clock.GetMinutes(), halfLifeMinutes);
            }
            else
            {
                return 0;
            }

        }

        void MergeRecord(uint key, UsageRecord record)
        {
            usage.AddOrUpdate(key, record, (k, oldValue) =>
            {
                if (oldValue.lastRead > record.lastRead)
                {
                    return oldValue;
                }
                else
                {
                    return record;
                }
            });
        }
        internal async Task MergeLoad(Stream stream, CancellationToken cancellationToken)
        {
            const int recordLength = 12;
            var buffer = new byte[recordLength * 200];
            int bytesRead;
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                for (int i = 0; i <= bytesRead - recordLength; i += recordLength)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var key = BitConverter.ToUInt32(buffer, i);
                    var record = new UsageRecord {
                        lastRead = BitConverter.ToUInt32(buffer, i + 4),
                        frequency = BitConverter.ToUInt32(buffer, i + 8)
                    };
                    MergeRecord(key, record);

                }

            } while (bytesRead > 0);
        }

        internal byte[] Serialize()
        {
            var data = new List<byte>((usage.Count + 10) * 12);
            var now = clock.GetMinutes();
            foreach (var pair in usage)
            {
                // Filter out frequencies below 2, they have expired 
                if (pair.Value.GetCurrentFrequency(now, halfLifeMinutes) > 1)
                {
                    data.AddRange(BitConverter.GetBytes(pair.Key));
                    data.AddRange(BitConverter.GetBytes(pair.Value.lastRead));
                    data.AddRange(BitConverter.GetBytes(pair.Value.frequency));
                }
            }
            return data.ToArray();
        }

    }
}
