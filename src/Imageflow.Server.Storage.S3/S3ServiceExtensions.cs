using Imageflow.Server.Extensibility.ClassicDiskCache;
using Imazen.Common.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.Storage.S3
{
    public static class S3ServiceExtensions
    {

        public static IServiceCollection AddImageflowDiskCache(this IServiceCollection services,
            S3ServiceSettings settings)
        {
            services.AddSingleton<IBlobProvider>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<S3Service>>();
                return new S3Service(settings, logger);
            });

            return services;
        }


    }
}