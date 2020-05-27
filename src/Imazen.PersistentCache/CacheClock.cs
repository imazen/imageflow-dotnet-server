using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Imazen.PersistentCache
{
    public class CacheClock : IClock
    {
        public long TicksPerSecond => Stopwatch.Frequency;

        public long GetTicks() => Stopwatch.GetTimestamp();

        public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

    }
}
