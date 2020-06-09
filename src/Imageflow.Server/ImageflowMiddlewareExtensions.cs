using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Imageflow.Server
{
    public static class ImageflowMiddlewareExtensions
    {
        
        public static IApplicationBuilder UseImageflow(this IApplicationBuilder builder, ImageflowMiddlewareSettings settings)
        {
            return builder.UseMiddleware<ImageflowMiddleware>(settings);
        }

    }
}
