using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Microsoft.Extensions.Hosting;

namespace Imageflow.Server.DiskCache
{
    internal class DiskCacheHostedServiceProxy: IHostedService
    {
        private readonly IClassicDiskCache cache;
        public DiskCacheHostedServiceProxy(IClassicDiskCache cache)
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