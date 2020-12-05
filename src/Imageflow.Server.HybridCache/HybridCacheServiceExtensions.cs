using Imazen.Common.Extensibility.StreamCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.HybridCache
{
    public static class HybridCacheServiceExtensions
    {
   

        public static IServiceCollection AddImageflowHybridCache(this IServiceCollection services, HybridCacheOptions options)
        {
            services.AddSingleton<IStreamCache>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<HybridCacheService>>();
                return new HybridCacheService(options, logger);
            });
            
            services.AddHostedService<HybridCacheHostedServiceProxy>();
            return services;
        }
    }
}