using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Issues;
using Imazen.HybridCache;
using Imazen.HybridCache.Sqlite;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.HybridSqliteCache
{
    public class HybridSqliteCacheService : IStreamCache
    {
        private readonly HybridCache cache;
        public HybridSqliteCacheService(HybridSqliteCacheOptions options, ILogger<HybridSqliteCacheService> logger)
        {
            var database = new SqliteCacheDatabase(new SqliteCacheDatabaseOptions(options.DatabaseDir), logger);
            cache = new HybridCache(database, new HybridCacheOptions(options.DiskCacheDir)
            {
                AsyncCacheOptions = new AsyncCacheOptions()
                {
                    MaxQueuedBytes = options.MaxWriteQueueBytes,
                    WriteSynchronouslyWhenQueueFull = true,
                },
                CleanupManagerOptions = new CleanupManagerOptions()
                {
                    MaxCacheBytes = options.CacheSizeLimitInBytes
                }
            }, logger);
        }

        public IEnumerable<IIssue> GetIssues()
        {
            return Enumerable.Empty<IIssue>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return cache.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return cache.StopAsync(cancellationToken);
        }

        public Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult dataProviderCallback, CancellationToken cancellationToken,
            bool retrieveContentType)
        {
            return cache.GetOrCreateBytes(key, dataProviderCallback, cancellationToken, retrieveContentType);
        }
    }
}