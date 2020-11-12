using System;
using System.Threading;
using System.Threading.Tasks;

namespace Imageflow.Server.Extensibility
{
    public interface ISqliteCache
    {
        Task<SqliteCacheEntry> GetOrCreate(string key, Func<Task<SqliteCacheEntry>> create);
        
        public Task StartAsync(CancellationToken cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken);

    }
}