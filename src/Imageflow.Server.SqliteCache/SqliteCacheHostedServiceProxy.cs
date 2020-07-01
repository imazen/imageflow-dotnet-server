using System.Threading;
using System.Threading.Tasks;
using Imageflow.Server.Extensibility;
using Microsoft.Extensions.Hosting;

namespace Imageflow.Server.SqliteCache
{
    internal class SqliteCacheHostedServiceProxy: IHostedService
    {
 
            private readonly ISqliteCache cache;
            public SqliteCacheHostedServiceProxy(ISqliteCache cache)
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