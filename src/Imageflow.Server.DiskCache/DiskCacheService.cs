using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.Common.Issues;
using Imazen.DiskCache;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.DiskCache
{
    public class DiskCacheService: IClassicDiskCache
    {
        private readonly DiskCacheOptions options;
        private readonly ClassicDiskCache cache;
        private readonly ILogger logger;
        public DiskCacheService(DiskCacheOptions options, ILogger logger)
        {
            this.options = options;
            cache = new ClassicDiskCache(options, logger );
            this.logger = logger;
        }


        public IEnumerable<IIssue> GetIssues()
        {
            return cache.GetIssues();
        }

        public Task<ICacheResult> GetOrCreate(string key, string fileExtension, AsyncWriteResult writeCallback)
        {
            return cache.GetOrCreate(key, fileExtension, writeCallback);
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