using Imazen.Abstractions.BlobCache;
using Imazen.Abstractions.Logging;
using Imazen.Common.Extensibility.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Imageflow.Server.HybridCache
{
    public static class HybridCacheServiceExtensions
    {
   

        public static IServiceCollection AddImageflowHybridCache(this IServiceCollection services, HybridCacheOptions options)
        {
            services.AddImageflowReLogStoreAndReLoggerFactoryIfMissing();
            services.AddSingleton<IBlobCacheProvider>((container) =>
            {
                var loggerFactory = container.GetRequiredService<IReLoggerFactory>();
                return new HybridCacheService(options, loggerFactory);
            });
            
            services.AddHostedService<HostedServiceProxy<IBlobCacheProvider>>();
            return services;
        }

        public static IServiceCollection AddImageflowHybridCaches(this IServiceCollection services, IEnumerable<HybridCacheOptions> namedCacheConfigurations)
        {
            services.AddSingleton<IBlobCacheProvider>((container) =>
            {
                var loggerFactory = container.GetRequiredService<IReLoggerFactory>();
                return new HybridCacheService(namedCacheConfigurations, loggerFactory);
            });
            
            services.AddHostedService<HostedServiceProxy<IBlobCacheProvider>>();
            return services;
        }
    }
}