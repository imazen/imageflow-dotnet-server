using System.Threading;
using System.Threading.Tasks;

namespace Imazen.HybridCache
{
    internal interface ICacheCleanupManager
    {
        void NotifyUsed(CacheEntry cacheEntry);
        Task<string> GetContentType(CacheEntry cacheEntry, CancellationToken cancellationToken);
        Task<bool> TryReserveSpace(CacheEntry cacheEntry, string contentType, int byteCount, bool allowEviction, CancellationToken cancellationToken);
    }
}