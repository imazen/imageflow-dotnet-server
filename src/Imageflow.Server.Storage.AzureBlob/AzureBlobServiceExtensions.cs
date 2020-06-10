using Imazen.Common.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.Storage.AzureBlob
{
    public static class AzureBlobServiceExtensions
    {

        public static IServiceCollection AddImageflowAzureBlobService(this IServiceCollection services,
            AzureBlobServiceOptions options)
        {
            services.AddSingleton<IBlobProvider>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<AzureBlobService>>();
                return new AzureBlobService(options, logger);
            });

            return services;
        }


    }
}