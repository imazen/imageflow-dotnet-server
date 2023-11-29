using Azure.Storage.Blobs;
using Imazen.Abstractions.Blobs.LegacyProviders;
using Imazen.Abstractions.Logging;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;

namespace Imageflow.Server.Storage.AzureBlob
{
    public static class AzureBlobServiceExtensions
    {

        public static IServiceCollection AddImageflowAzureBlobService(this IServiceCollection services,
            AzureBlobServiceOptions options)
        {
            services.AddImageflowReLogStoreAndReLoggerFactoryIfMissing();
            services.AddSingleton<IBlobWrapperProvider>((container) =>
            {
                var loggerFactory = container.GetRequiredService<IReLoggerFactory>();
                var blobServiceClient = container.GetService<BlobServiceClient>();
                var clientFactory = container.GetService<IAzureClientFactory<BlobServiceClient>>();
              
                return new AzureBlobService(options, loggerFactory, blobServiceClient, clientFactory);
            });

            return services;
        }


    }
}