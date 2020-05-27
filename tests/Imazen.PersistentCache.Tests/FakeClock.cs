using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Imazen.PersistentCache.Tests
{
    class FakeClock : IClock
    {
        DateTimeOffset now;

        public FakeClock(string date)
        {
            now = DateTimeOffset.Parse(date);
        }

        public void AdvanceSeconds(long seconds) { now = now.AddSeconds(seconds); }
        public DateTimeOffset GetUtcNow() => now;
        public long GetTicks() => now.Ticks;
        public long TicksPerSecond { get; } = Stopwatch.Frequency;
    }
}
