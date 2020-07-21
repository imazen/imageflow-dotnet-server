using System.IO;
using Amazon;
using Azure.Storage.Blobs;
using Imageflow.Fluent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Imageflow.Server.DiskCache;
using Imageflow.Server.Storage.AzureBlob;
using Imageflow.Server.Storage.S3;
using System;
using Imageflow.Server.SqliteCache;
using Imageflow.Server.Storage.RemoteReader;

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
            
            services.AddImageflowRemoteReaderService(new RemoteReaderServiceOptions { SigningKey = "ChangeMe"}
                .AddPrefix("/remote/")
                );

            // Make S3 containers available at /ri/ and /imageflow-resources/
            // If you use credentials, do not check them into your repository
            // You can call AddImageflowS3Service multiple times for each unique access key
            services.AddImageflowS3Service(new S3ServiceOptions( null,null)
                .MapPrefix("/ri/", RegionEndpoint.USEast1, "resizer-images")
                .MapPrefix("/imageflow-resources/", RegionEndpoint.USWest2, "imageflow-resources"));
            
            // Make Azure container available at /azure
            // You can call AddImageflowAzureBlobService multiple times for each connection string
            services.AddImageflowAzureBlobService(
                new AzureBlobServiceOptions(
                        "UseDevelopmentStorage=true;",
                        new BlobClientOptions())
                    .MapPrefix("/azure", "imageflow-demo" ));

            var homeFolder = (Environment.OSVersion.Platform == PlatformID.Unix ||
                   Environment.OSVersion.Platform == PlatformID.MacOSX)
                    ? Environment.GetEnvironmentVariable("HOME")
                    : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

            // You can add a distributed cache, such as redis, if you add it and and
            // call ImageflowMiddlewareOptions.SetAllowDistributedCaching(true)
            services.AddDistributedMemoryCache();
            // You can add a memory cache and call ImageflowMiddlewareOptions.SetAllowMemoryCaching(true)
            services.AddMemoryCache();
            // You can add a disk cache and call ImageflowMiddlewareOptions.SetAllowDiskCaching(true)
            // If you're deploying to azure, provide a disk cache folder *not* inside ContentRootPath
            // to prevent the app from recycling whenever folders are created.
            services.AddImageflowDiskCache(new DiskCacheOptions(Path.Combine(homeFolder, "imageflow_example_cache")));
            //services.AddImageflowSqliteCache(
            //    new SqliteCacheOptions(Path.Combine(homeFolder, "imageflow_example_sqlite_cache")));
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

            // You have a lot of configuration options
            app.UseImageflow(new ImageflowMiddlewareOptions()
                // Maps / to WebRootPath
                .SetMapWebRoot(true)
                // Maps /folder to WebRootPath/folder
                .MapPath("/folder", Path.Combine(Env.ContentRootPath, "folder"))
                // Allow Disk Caching
                .SetAllowDiskCaching(true)
                // Allow Sqlite Caching
                .SetAllowSqliteCaching(false)
                // We can only have one type of caching enabled at a time
                .SetAllowDistributedCaching(false)
                // Disable memory caching even if the service is installed
                .SetAllowMemoryCaching(false)
                // Cache publicly (including on shared proxies and CDNs) for 30 days
                .SetDefaultCacheControlString("public, max-age=2592000")
                // Force all paths under "/gallery" to be watermarked
                .AddRewriteHandler("/gallery", args =>
                {
                    args.Query["watermark"] = "imazen";
                })
                .AddCommandDefault("down.filter", "mitchell")
                .AddCommandDefault("f.sharpen", "15")
                .AddCommandDefault("webp.quality", "90")
                .AddCommandDefault("ignore_icc_errors", "true")
                //When set to true, this only allows ?preset=value URLs, returning 403 if you try to use any other commands. 
                .SetUsePresetsExclusively(false)
                .AddPreset(new PresetOptions("large", PresetPriority.DefaultValues)
                    .SetCommand("width", "1024")
                    .SetCommand("height", "1024")
                    .SetCommand("mode", "max"))
                // It's a good idea to limit image sizes for security. Requests causing these to be exceeded will fail
                // The last argument to FrameSizeLimit() is the maximum number of megapixels
                .SetJobSecurityOptions(new SecurityOptions()
                    .SetMaxDecodeSize(new FrameSizeLimit(8000,8000, 40))
                    .SetMaxFrameSize(new FrameSizeLimit(8000,8000, 40))
                    .SetMaxEncodeSize(new FrameSizeLimit(8000,8000, 20)))
                // Register a named watermark that floats 10% from the bottom-right corner of the image
                // With 70% opacity and some sharpness applied. 
                .AddWatermark(
                    new NamedWatermark("imazen",
                        "/images/imazen_400.png",
                        new WatermarkOptions()
                            .SetFitBoxLayout(
                                new WatermarkFitBox(WatermarkAlign.Image, 10, 10, 90, 90),
                                WatermarkConstraintMode.Within,
                                new ConstraintGravity(100, 100))
                            .SetOpacity(0.7f)
                            .SetHints(
                                new ResampleHints()
                                    .SetResampleFilters(InterpolationFilter.Robidoux_Sharp, null)
                                    .SetSharpen(7, SharpenWhen.Downscaling))
                            .SetMinCanvasSize(200, 150))));
            
            
            
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
