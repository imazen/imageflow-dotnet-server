using System.IO;
using Imageflow.Fluent;
using Imageflow.Server.HybridCache;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Imageflow.Server.ExampleDockerDiskCache
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            Env = env;
        }

        private IConfiguration Configuration { get; }
        private IWebHostEnvironment Env { get; }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddImageflowHybridCache(new HybridCacheOptions(Path.Combine(Env.ContentRootPath, "imageflow_cache"))
            {
                CacheSizeLimitInBytes = (long)1 * 1024 * 1024 * 1024 //1 GiB
            });
        }
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }else
            {
                app.UseExceptionHandler("/error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //app.UseHttpsRedirection();

            app.UseImageflow(new ImageflowMiddlewareOptions()
                // Only use this if this is a legitimate AGPL-compliant project, otherwise uncomment .SetLicenseKey
                .SetMyOpenSourceProjectUrl("https://please-support-imageflow-with-a-license.com")
                //.SetLicenseKey(EnforceLicenseWith.Http402Error, "license key here")
                
                // Remove the following if you don't have a wwwroot folder and want to serve images from it
                .SetMapWebRoot(true)
                
                // Change the following line to map a different virtual path to a physical folder
                .MapPath("/images", Path.Combine(Env.ContentRootPath, "images"))
                
                // Allow HybridCache or other registered IStreamCache to run
                .SetAllowCaching(true)
                
                // Allow localhost to access the diagnostics page (or always, if in development)
                .SetDiagnosticsPageAccess(env.IsDevelopment() ? AccessDiagnosticsFrom.AnyHost : AccessDiagnosticsFrom.LocalHost)
                
                // Uncomment the following to allow remote diagnostics access via /imageflow.debug?password=fuzzy_caterpillar
                //.SetDiagnosticsPagePassword("fuzzy_caterpillar")
                
                // Uncomment to allow HTTP caching, publicly (including on shared proxies and CDNs) for 30 days
                //.SetDefaultCacheControlString("public, max-age=2592000")
                
                // Uncomment the following to change the default downscaling filter to be a bit more ImageResizer 4-like
                // .AddCommandDefault("down.filter", "mitchell")
                
                // Uncomment the following to change the default downscaling filter to erase highlights and darken shadows, like ImageResizer 4 does
                // .AddCommandDefault("down.colorspace", "srgb")
                
                // Uncomment the following for sharper images by default
                // .AddCommandDefault("f.sharpen", "15")
                
                // Uncomment the following to lower files sizes and increase WebP compression by default
                // .AddCommandDefault("webp.quality", "60")
                
                // Uncomment the following to lower file sizes and increase JPEG compression by default
                // .AddCommandDefault("quality", "76")
                
                
                .SetJobSecurityOptions(new SecurityOptions()
                    // Adjust the following to permit images over 40 megapixels to be processed, or to permit images with a dimension over 8000
                    .SetMaxDecodeSize(new FrameSizeLimit(8000, 8000, 40))
                    .SetMaxFrameSize(new FrameSizeLimit(8000, 8000, 40))
                    // Adjust the following to allow final images to be encoded in sizes greater than 20 megapixelsx
                    .SetMaxEncodeSize(new FrameSizeLimit(8000, 8000, 20)))
            );
            
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                // You can remove this endpoint to disable the sample image
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync("<img src=\"fire-umbrella-small.jpg?width=450\" />");
                });
                
                endpoints.MapGet("/error", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync("<p>An error has occurred while processing the request.</p>");
                });
            });
        }
    }
}