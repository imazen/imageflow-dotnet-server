using System;
using System.Collections.Generic;
using System.Text;

namespace Imazen.PersistentCache
{
    public interface IClock
    {
        DateTimeOffset GetUtcNow();
        long GetTicks();
        long TicksPerSecond { get; }


    }
}
