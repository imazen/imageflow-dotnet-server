using Imazen.Common.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace Imageflow.Server.Storage.RemoteReader
{
    public static class RemoteReaderServiceExtensions
    {
        public static IServiceCollection AddImageflowRemoteReaderService(this IServiceCollection services,
    RemoteReaderServiceOptions options)
        {
            services.AddSingleton<IBlobProvider>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<RemoteReaderService>>();
                var factory = container.GetRequiredService<IHttpClientFactory>();
                var http = factory.CreateClient(options.HttpClientName ?? "");
                return new RemoteReaderService(options, logger, http);
            });

            return services;
        }
    }
}
