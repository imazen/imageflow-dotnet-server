using System;
using System.Collections.Generic;
using System.IO;
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
                    MaxQueuedBytes = Math.Max(0,options.QueueSizeLimitInBytes),
                    WriteSynchronouslyWhenQueueFull = true,
                    MoveFileOverwriteFunc = (from, to) => File.Move(from, to, true)
                },
                CleanupManagerOptions = new CleanupManagerOptions()
                {
                    MaxCacheBytes = Math.Max(0, options.CacheSizeLimitInBytes),
                    MinCleanupBytes = Math.Max(0, options.MinCleanupBytes),
                    MinAgeToDelete = options.MinAgeToDelete.Ticks > 0 ? options.MinAgeToDelete : TimeSpan.Zero,
                }
            };
            var database = new MetaStore(new MetaStoreOptions(options.DiskCacheDirectory)
            {
                Shards = Math.Max(1, options.DatabaseShards),
                MaxLogFilesPerShard = 3,
            }, cacheOptions, logger);
            cache = new Imazen.HybridCache.HybridCache(database, cacheOptions, logger);
        }

        public IEnumerable<IIssue> GetIssues()
        {
            return cache.GetIssues();
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