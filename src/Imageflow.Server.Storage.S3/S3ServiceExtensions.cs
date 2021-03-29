using Amazon.S3;
using Imazen.Common.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.Storage.S3
{
    public static class S3ServiceExtensions
    {

        // ReSharper disable once UnusedMethodReturnValue.Global
        public static IServiceCollection AddImageflowS3Service(this IServiceCollection services,
            S3ServiceOptions options)
        {
            services.AddSingleton<IBlobProvider>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<S3Service>>();
                var s3 = container.GetRequiredService<IAmazonS3>();
                return new S3Service(options, s3, logger);
            });

            return services;
        }


    }
}