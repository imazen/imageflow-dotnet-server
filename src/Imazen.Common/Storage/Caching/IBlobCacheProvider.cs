using System.Collections.Generic;

namespace Imazen.Common.Storage.Caching
{

    public interface IBlobCacheProvider
    {
        bool TryGetCache(string name, out IBlobCache cache);
        IEnumerable<string> GetCacheNames();
    }
}
