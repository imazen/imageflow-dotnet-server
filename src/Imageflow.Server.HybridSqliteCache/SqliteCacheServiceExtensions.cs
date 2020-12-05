
using Imazen.Common.Extensibility.StreamCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.HybridSqliteCache
{
    public static class SqliteCacheServiceExtensions
    {
   

        public static IServiceCollection AddImageflowHybridSqliteCache(this IServiceCollection services, HybridSqliteCacheOptions options)
        {
            services.AddSingleton<IStreamCache>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<HybridSqliteCacheService>>();
                return new HybridSqliteCacheService(options, logger);
            });
            
            services.AddHostedService<HybridSqliteCacheHostedServiceProxy>();
            return services;
        }
    }
}