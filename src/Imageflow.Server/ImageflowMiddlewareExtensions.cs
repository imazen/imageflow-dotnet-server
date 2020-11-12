using Microsoft.AspNetCore.Builder;

namespace Imageflow.Server
{
    public static class ImageflowMiddlewareExtensions
    {
        
        // ReSharper disable once UnusedMethodReturnValue.Global
        public static IApplicationBuilder UseImageflow(this IApplicationBuilder builder, ImageflowMiddlewareOptions options)
        {
            return builder.UseMiddleware<ImageflowMiddleware>(options);
        }

    }
}
