using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Imageflow.Server.Extensibility.ClassicDiskCache;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.Common.Issues;
using Imazen.DiskCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.DiskCache
{
    public class DiskCacheService: IClassicDiskCache
    {
        private readonly DiskCacheSettings settings;
        private readonly ClassicDiskCache cache;
        private readonly ILogger logger;
        public DiskCacheService(DiskCacheSettings settings, ILogger logger)
        {
            this.settings = settings;
            this.cache = new ClassicDiskCache(settings, logger );
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
            if (!cache.Start())
            {
                throw new InvalidOperationException("DiskCache configuration invalid");
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cache.Stop();
            return Task.CompletedTask;
        }
    }
}