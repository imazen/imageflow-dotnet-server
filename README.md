[![Imageflow.Server Nuget Packages](https://github.com/imazen/imageflow-dotnet-server/workflows/Imageflow.Server%20Nuget%20Packages/badge.svg)](https://github.com/imazen/imageflow-dotnet-server/actions?query=workflow%3A%22Imageflow.Server+Nuget+Packages%22)

Imageflow.NET Server is image processing and optimizing middleware for ASP.NET Core 3.1+.
 
Under the hood, it uses Imageflow, the fastest image handling library for web servers. 
Imageflow focuses on security, quality, and performance - in that order.

Supports

* Windows, Mac, and Linux
* Processing images located on Azure Blob Storage or Amazon S3
* Disk Caching
* Memory Caching
* Distributed Caching
* Watermarking
* Mapping arbitrary virtual paths to physical ones. 
* Imageflow's [Querystring API](https://docs.imageflow.io/querystring/introduction.html)


```
PM> Install-Package Imageflow.Server
PM> Install-Package Imageflow.Server.DiskCache
PM> Install-Package Imageflow.Server.Storage.S3
PM> Install-Package Imageflow.Server.Storage.AzureBlob
PM> Install-Package Imageflow.NativeRuntime.win-x86 
PM> Install-Package Imageflow.NativeRuntime.win-x86_64
PM> Install-Package Imageflow.NativeRuntime.osx_10_11-x86_64
PM> Install-Package Imageflow.NativeRuntime.ubuntu_16_04-x86_64 
PM> Install-Package Imageflow.NativeRuntime.ubuntu_18_04-x86_64
```

```c#
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
            
            // Make S3 containers available at /ri/ and /imageflow-resources/
            // If you use credentials, do not check them into your repository
            services.AddImageflowS3Service(new S3ServiceOptions( RegionEndpoint.USEast1, null,null)
                .MapPrefix("/ri/", "us-east-1", "resizer-images")
                .MapPrefix("/imageflow-resources/", "us-west-2", "imageflow-resources"));

            // Make Azure container available at /azure
            services.AddImageflowAzureBlobService(
                new AzureBlobServiceOptions(
                        "UseDevelopmentStorage=true;",
                        new BlobClientOptions())
                    .MapPrefix("/azure", "imageflow-demo" ));

            // You can add a distributed cache, such as redis, if you add it and and
            // call ImageflowMiddlewareOptions.SetAllowDistributedCaching(true)
            services.AddDistributedMemoryCache();
            // You can add a memory cache and call ImageflowMiddlewareOptions.SetAllowMemoryCaching(true)
            services.AddMemoryCache();
            // You can add a disk cache and call ImageflowMiddlewareOptions.SetAllowDiskCaching(true)
            // If you're deploying to azure, provide a disk cache folder *not* inside ContentRootPath
            // to prevent the app from recycling whenever folders are created.
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

            // You have a lot of configuration options
            app.UseImageflow(new ImageflowMiddlewareOptions()
                // Maps / to WebRootPath
                .SetMapWebRoot(true) 
                // Maps /folder to WebRootPath/folder
                .MapPath("/folder", Path.Combine(Env.ContentRootPath, "folder"))
                // Allow Disk Caching
                .SetAllowDiskCaching(true)
                // We can only have one type of caching enabled at a time
                .SetAllowDistributedCaching(false)
                // Disable memory caching even if the service is installed
                .SetAllowMemoryCaching(false)
                // Cache publicly (including on shared proxies and CDNs) for 30 days
                .SetDefaultCacheControlString("public, max-age=2592000")
                // Register a named watermark that floats 10% from the bottom-right corner of the image
                // With 70% opacity and some sharpness applied. 
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

```

Note: You must install the [appropriate NativeRuntime(s)](https://www.nuget.org/packages?q=Imageflow+AND+NativeRuntime) in the project you are deploying - they have to copy imageflow.dll to the output folder. 

[NativeRuntimes](https://www.nuget.org/packages?q=Imageflow+AND+NativeRuntime) that are suffixed with -haswell (2013, AVX2 support) require a CPU of that generation or later. 

