[![Build status](https://ci.appveyor.com/api/projects/status/5hm0ekhe455i56fp/branch/master?svg=true)](https://ci.appveyor.com/project/imazen/imageflow-dotnet-server/branch/master)

Imageflow.NET Server is image processing and optimizing middleware for ASP.NET Core 3.1+. 
If you don't need an HTTP server, [try Imageflow.NET](https://github.com/imazen/imageflow-dotnet).
 
Under the hood, it uses [Imageflow](https://imageflow.io), the fastest image handling library for web servers. 
Imageflow focuses on security, quality, and performance - in that order.

### Features

* Supports Windows, Mac, and Linux
* Processing images located on disk, Azure Blob Storage or Amazon S3
* Disk Caching
* Memory Caching
* Distributed Caching
* Watermarking
* Mapping arbitrary virtual paths to physical ones. 
* Imageflow's [Querystring API](https://docs.imageflow.io/querystring/introduction.html) (compatible with ImageResizer)

## Basic Installation

You can look at `examples/Imageflow.Server.ExampleMinimal` to see the result. 

1. Create a new ASP.NET Core 3.1 project using the Empty template. 
2. Create a directory called "wwwroot" and add a file "image.jpg"
3. Install both `Imageflow.Server` and all `Imageflow.NativeRuntime.*` packages for platforms you are targeting. 
    ```
    Install-Package Imageflow.Server
    Install-Package Imageflow.NativeRuntime.win-x86 -pre
    Install-Package Imageflow.NativeRuntime.win-x86_64 -pre
    Install-Package Imageflow.NativeRuntime.osx_10_11-x86_64 -pre
    Install-Package Imageflow.NativeRuntime.ubuntu_16_04-x86_64 -pre
    Install-Package Imageflow.NativeRuntime.ubuntu_18_04-x86_64 -pre
    ```
4. Open Startup.cs and edit the Configure method.  Add
    ```c#
    app.UseImageflow(new ImageflowMiddlewareOptions()
        .SetMapWebRoot(true));
    ```
5. Replace the endpoint with something that generates an image tag, like 
   ```c#
   app.UseEndpoints(endpoints =>
   {
       endpoints.MapGet("/", async context =>
       {
           context.Response.ContentType = "text/html";
           await context.Response.WriteAsync("<img src=\"fire-umbrella-small.jpg?width=450\" />");
       });
   });
   ```
6. Run your project and see the image be dynamically resized. 

## Installing everything

See `examples/Imageflow.Server.Example` for this example. 

```
Install-Package Imageflow.Server
Install-Package Imageflow.Server.DiskCache
Install-Package Imageflow.Server.Storage.S3
Install-Package Imageflow.Server.Storage.AzureBlob
Install-Package Imageflow.NativeRuntime.win-x86 -pre
Install-Package Imageflow.NativeRuntime.win-x86_64 -pre
Install-Package Imageflow.NativeRuntime.osx_10_11-x86_64 -pre
Install-Package Imageflow.NativeRuntime.ubuntu_16_04-x86_64 -pre
Install-Package Imageflow.NativeRuntime.ubuntu_18_04-x86_64 -pre
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
            services.AddImageflowS3Service(new S3ServiceOptions(null, null)
                .MapPrefix("/ri/", RegionEndpoint.USEast1, "resizer-images")
                .MapPrefix("/imageflow-resources/", RegionEndpoint.USWest2, "imageflow-resources"));

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
                // Maps /folder to ContentRootPath/folder
                .MapPath("/folder", Path.Combine(Env.ContentRootPath, "folder"))
                // Allow Disk Caching
                .SetAllowDiskCaching(true)
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
                // Register a named watermark that floats 10% from the bottom-right corner of the image
                // With 70% opacity and some sharpness applied. 
                .AddWatermark(
                    new NamedWatermark("imazen", 
                        "/images/imazen_400.png",
                        new WatermarkOptions()
                            .SetFitBoxLayout(
                                new WatermarkFitBox(WatermarkAlign.Image, 10,10,90,90), 
                                WatermarkConstraintMode.Within, 
                                new ConstraintGravity(100,100) )
                            .SetOpacity(0.7f)
                            .SetHints(
                                new ResampleHints()
                                    .SetResampleFilters(InterpolationFilter.Robidoux_Sharp, null)
                                    .SetSharpen(7, SharpenWhen.Downscaling))
                            .SetMinCanvasSize(300,300))));
            
            
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

