using Imageflow.Server.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.SqliteCache
{
    public static class SqliteCacheServiceExtensions
    {
   

        public static IServiceCollection AddImageflowSqliteCache(this IServiceCollection services, SqliteCacheOptions options)
        {
            services.AddSingleton<ISqliteCache>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<SqliteCacheService>>();
                return new SqliteCacheService(options, logger);
            });
            
            services.AddHostedService<SqliteCacheHostedServiceProxy>();
            return services;
        }
    }
}