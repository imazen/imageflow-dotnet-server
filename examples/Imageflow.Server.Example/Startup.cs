using System.IO;
using Amazon;
using Imageflow.Fluent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Imageflow.Server;
using Imageflow.Server.DiskCache;
using Imageflow.Server.Storage.S3;
using Microsoft.Extensions.Hosting.Internal;

namespace Imageflow.Server.Example
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

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddImageflowS3Service(new S3ServiceOptions( RegionEndpoint.USEast1, null,null)
                .MapPrefix("/ri/", "us-east-1", "resizer-images")
                .MapPrefix("/imageflow-resources/", "us-west-2", "imageflow-resources"));
            services.AddMemoryCache();
            services.AddImageflowDiskCache(new DiskCacheOptions(Path.Combine(Env.ContentRootPath, "imageflow_cache")));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            
            app.UseHttpsRedirection();
            app.UseImageflow(new ImageflowMiddlewareOptions()
                .SetMapWebRoot(true)
                .SetAllowDiskCaching(false)
                .SetDefaultCacheControlString("public, max-age=2592000")
                .AddWatermark(
                    new NamedWatermark("imazen", 
                        "/images/imazen_400.png",
                        new WatermarkOptions()
                            .LayoutWithFitBox(
                                new WatermarkFitBox(WatermarkAlign.Image, 10,10,90,90), 
                                WatermarkConstraintMode.Within, 
                                new ConstraintGravity(100,100) )
                            .WithOpacity(0.7f)
                            .WithHints(
                                new ResampleHints()
                                    .ResampleFilter(InterpolationFilter.Robidoux_Sharp, null)
                                    .Sharpen(7, SharpenWhen.Downscaling)))));
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
