using Imazen.Abstractions.BlobCache;
using Imazen.Abstractions.Logging;
using Imazen.Common.Extensibility.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Imageflow.Server.HybridCache
{
    public static class HybridCacheServiceExtensions
    {
   
        // public static IServiceCollection AddImageflowHybridCache(this IServiceCollection services, HybridCacheOptions options)
        // {
        //     services.AddImageflowReLogStoreAndReLoggerFactoryIfMissing();
        //
        //     HybridCacheService? captured = null;
        //     services.AddSingleton<IBlobCacheProvider>((container) =>
        //     {
        //         var loggerFactory = container.GetRequiredService<IReLoggerFactory>();
        //         captured = new HybridCacheService(options, loggerFactory);
        //         return captured;
        //     });
        //     services.AddSingleton<IHostedService>(container => (IHostedService)container.GetServices<IBlobCacheProvider>().Where(c => c == captured).Single() 
        //     
        //     services.AddHostedService<HostedServiceProxy<IBlobCacheProvider>>();
        //     return services;
        // }


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