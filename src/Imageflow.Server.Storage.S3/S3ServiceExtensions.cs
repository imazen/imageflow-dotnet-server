using Amazon.S3;
using Imazen.Abstractions.Blobs.LegacyProviders;
using Imazen.Abstractions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Imageflow.Server.Storage.S3
{
    public static class S3ServiceExtensions
    {

        // ReSharper disable once UnusedMethodReturnValue.Global
        public static IServiceCollection AddImageflowS3Service(this IServiceCollection services,
            S3ServiceOptions options)
        {
            services.AddImageflowReLogStoreAndReLoggerFactoryIfMissing();
            services.AddSingleton<IBlobWrapperProvider>((container) =>
            {
                var loggerFactory = container.GetRequiredService<IReLoggerFactory>();
                var s3 = container.GetRequiredService<IAmazonS3>();
                return new S3Service(options, s3, loggerFactory);
            });

            return services;
        }


    }
}