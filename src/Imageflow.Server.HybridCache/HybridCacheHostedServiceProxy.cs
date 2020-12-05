using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.StreamCache;
using Microsoft.Extensions.Hosting;

namespace Imageflow.Server.HybridCache
{
    internal class HybridCacheHostedServiceProxy: IHostedService
    {
 
            private readonly IStreamCache cache;
            public HybridCacheHostedServiceProxy(IStreamCache cache)
            {
                this.cache = cache;
            }
            public Task StartAsync(CancellationToken cancellationToken)
            {
                return cache.StartAsync(cancellationToken);
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return cache.StopAsync(cancellationToken);
            }
        
    }
}