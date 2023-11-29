using Imazen.Abstractions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server
{
    public static class ImageflowMiddlewareExtensions
    {

        public static IServiceCollection AddImageflowLoggingSupport(this IServiceCollection services, ReLogStoreOptions? logStorageOptions = null)
        {
            services.AddSingleton<IReLogStore>((container) => new ReLogStore(logStorageOptions ?? new ReLogStoreOptions()));
            services.AddSingleton<IReLoggerFactory>((container) => 
                new ReLoggerFactory(container.GetRequiredService<ILoggerFactory>(), container.GetRequiredService<IReLogStore>()));
            return services;
        }


        // ReSharper disable once UnusedMethodReturnValue.Global
        public static IApplicationBuilder UseImageflow(this IApplicationBuilder builder, ImageflowMiddlewareOptions options)
        {
            if (builder.ApplicationServices.GetService<IReLoggerFactory>() == null)
            {
                var services = builder.ApplicationServices as IServiceCollection;
                services?.AddImageflowLoggingSupport();
            }
            return builder.UseMiddleware<ImageflowMiddleware>(options);
        }

    }
}
