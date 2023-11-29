using System;
using Imazen.Abstractions.BlobCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Imazen.Common.Extensibility.Support;

namespace Imageflow.Server.DiskCache
{
    public static class DiskCacheServiceExtensions
    {
        public static IServiceCollection AddImageflowDiskCache(this IServiceCollection services, DiskCacheOptions options)
        {
            throw new NotSupportedException("Imageflow.Server.DiskCache is no longer supported. Use Imageflow.Server.HybridCache instead.");
            services.AddSingleton<IBlobCache>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<DiskCacheService>>();
                return new DiskCacheService(options, logger);
            });
            
            // crucial detail - otherwise IHostedService methods won't be called and stuff will break silently and terribly
            services.AddHostedService<HostedServiceProxy<IBlobCache>>();
            return services;
        }

    }
}