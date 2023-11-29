using Imazen.Abstractions.Logging;

namespace Imageflow.Server.HybridCache
{
    internal class HybridNamedCache : Imazen.HybridCache.HybridCache
    {
        public HybridNamedCache(HybridCacheOptions options, IReLoggerFactory loggerFactory): base(options, loggerFactory)
        {}
       
    }
}
