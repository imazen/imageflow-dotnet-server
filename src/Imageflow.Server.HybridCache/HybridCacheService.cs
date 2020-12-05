using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Issues;
using Imazen.HybridCache;
using Imazen.HybridCache.MetaStore;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.HybridCache
{
    public class HybridCacheService : IStreamCache
    {
        private readonly Imazen.HybridCache.HybridCache cache;
        public HybridCacheService(HybridCacheOptions options, ILogger<HybridCacheService> logger)
        {

            var cacheOptions = new Imazen.HybridCache.HybridCacheOptions(options.DiskCacheDirectory)
            {
                AsyncCacheOptions = new AsyncCacheOptions()
                {
                    MaxQueuedBytes = options.MaxWriteQueueBytes,
                    WriteSynchronouslyWhenQueueFull = true,
                },
                CleanupManagerOptions = new CleanupManagerOptions()
                {
                    MaxCacheBytes = options.CacheSizeLimitInBytes,
                    MinCleanupBytes = options.MinCleanupBytes,
                    MinAgeToDelete = options.MinAgeToDelete,
                }
            };
            var database = new Imazen.HybridCache.MetaStore.MetaStore(new MetaStoreOptions(options.DiskCacheDirectory)
            {
                Shards = 16,
                MaxLogFilesPerShard = 3,
            }, cacheOptions, logger);
            cache = new Imazen.HybridCache.HybridCache(database,cacheOptions , logger);
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