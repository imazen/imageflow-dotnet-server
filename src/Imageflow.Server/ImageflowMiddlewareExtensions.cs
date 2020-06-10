using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Imageflow.Server
{
    public static class ImageflowMiddlewareExtensions
    {
        
        public static IApplicationBuilder UseImageflow(this IApplicationBuilder builder, ImageflowMiddlewareOptions options)
        {
            return builder.UseMiddleware<ImageflowMiddleware>(options);
        }

    }
}
