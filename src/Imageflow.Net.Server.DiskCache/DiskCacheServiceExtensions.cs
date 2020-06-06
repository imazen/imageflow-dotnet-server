using System;
using Imageflow.Server.Extensibility.ClassicDiskCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.DiskCache
{
    public static class DiskCacheServiceExtensions
    {
        public static IServiceCollection AddImageflowDiskCache(this IServiceCollection services, DiskCacheSettings settings)
        {
            services.AddSingleton<IClassicDiskCache>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<DiskCacheService>>();
                return new DiskCacheService(settings, logger);
            });
            
         
            
                
            services.AddHostedService<DiskCacheHostedServiceProxy>();
            return services;
        }

    }
}