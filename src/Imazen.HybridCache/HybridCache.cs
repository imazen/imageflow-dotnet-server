using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Issues;
using Microsoft.Extensions.Logging;

namespace Imazen.HybridCache
{
    public class HybridCache : IStreamCache
    {
        private readonly ILogger logger;
        private HashBasedPathBuilder PathBuilder { get; }
        internal AsyncCache AsyncCache { get; }
        private CleanupManager CleanupManager { get; }
        private ICacheDatabase Database { get; }
        
        
        public HybridCache(ICacheDatabase cacheDatabase, HybridCacheOptions options, ILogger logger)
        {
            this.logger = logger;
            Database = cacheDatabase;
            PathBuilder = new HashBasedPathBuilder(options.PhysicalCacheDir, options.Subfolders,
                Path.DirectorySeparatorChar, ".jpg");
            options.CleanupManagerOptions.MoveFileOverwriteFunc = options.CleanupManagerOptions.MoveFileOverwriteFunc ??
                                                                  options.AsyncCacheOptions.MoveFileOverwriteFunc;
            
            CleanupManager = new CleanupManager(options.CleanupManagerOptions, Database, logger, PathBuilder);
            AsyncCache = new AsyncCache(options.AsyncCacheOptions, CleanupManager,PathBuilder, logger);
        }
        
        public IEnumerable<IIssue> GetIssues()
        {
            return Enumerable.Empty<IIssue>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Database.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            logger?.LogInformation("HybridCache is shutting down...");
            var sw = Stopwatch.StartNew();
            await AsyncCache.AwaitEnqueuedTasks();
            await Database.StopAsync(cancellationToken);
            sw.Stop();
            logger?.LogInformation("HybridCache shut down in {ShutdownTime}", sw.Elapsed);
        }

        public Task AwaitEnqueuedTasks()
        {
            return AsyncCache.AwaitEnqueuedTasks();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="dataProviderCallback"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="retrieveContentType"></param>
        /// <returns></returns>
        public async Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult dataProviderCallback, CancellationToken cancellationToken, bool retrieveContentType)
        {
            return await AsyncCache.GetOrCreateBytes(key, dataProviderCallback, cancellationToken, retrieveContentType);
        }
    }
}