using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Imageflow.Server.Extensibility
{
    public delegate Task AsyncCacheWrite(Stream output);

    
    public interface ISqliteCache
    {
        Task<SqliteCacheEntry> GetOrCreate(string key, Func<Task<SqliteCacheEntry>> create);
        
        public Task StartAsync(CancellationToken cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken);

    }
}