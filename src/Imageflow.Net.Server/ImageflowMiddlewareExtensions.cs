using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Imageflow.Server
{
    public static class ImageflowMiddlewareExtensions
    {

        public static IServiceCollection AddImageflow(this IServiceCollection services)
        {
            return services.AddMemoryCache();
        }

        public static IApplicationBuilder UseImageflow(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ImageflowMiddleware>();
        }

    }
}
