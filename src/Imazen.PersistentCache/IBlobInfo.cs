using System;
using System.Collections.Generic;
using System.Text;

namespace Imazen.PersistentCache
{
    public interface IBlobInfo
    {
        string KeyName { get; }
        ulong SizeInBytes { get; }
    }
}
