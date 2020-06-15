using System;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using Amazon;
using Imageflow.Fluent;
using Imageflow.Server.DiskCache;
using Imageflow.Server.Storage.S3;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Imageflow.Server.Tests
{
    public class IntegrationTest
    {

        [Fact]
        public async void TestLocalFiles()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/logo.png", "TestFiles.imazen_400.png"))
            {

                var hostBuilder = new HostBuilder()
                    .ConfigureWebHost(webHost =>
                    {
                        // Add TestServer
                        webHost.UseTestServer();
                        webHost.Configure(app =>
                        {
                            app.UseImageflow(new ImageflowMiddlewareOptions()
                                .SetMapWebRoot(false)
                                // Maps / to ContentRootPath/images
                                .MapPath("/", Path.Combine(contentRoot.PhysicalPath, "images"))
                                .AddWatermark(new NamedWatermark("imazen", "/logo.png", new WatermarkOptions()))
                                .AddWatermark(new NamedWatermark("broken", "/not_there.png", new WatermarkOptions())));
                        });
                    });

                // Build and start the IHost
                using var host = await hostBuilder.StartAsync();

                // Create an HttpClient to send requests to the TestServer
                using var client = host.GetTestClient();

                using var notFoundResponse = await client.GetAsync("/not_there.jpg");
                Assert.Equal(HttpStatusCode.NotFound,notFoundResponse.StatusCode);
                
                using var watermarkBrokenResponse = await client.GetAsync("/fire.jpg?watermark=broken");
                Assert.Equal(HttpStatusCode.NotFound,watermarkBrokenResponse.StatusCode);
                
                
                using var watermarkResponse = await client.GetAsync("/fire.jpg?watermark=imazen");
                watermarkResponse.EnsureSuccessStatusCode();
                
                using var response2 = await client.GetAsync("/fire.jpg?width=1");
                response2.EnsureSuccessStatusCode();
                var responseBytes = await response2.Content.ReadAsByteArrayAsync();
                Assert.True(responseBytes.Length < 1000);
                
                using var response3 = await client.GetAsync("/fire.jpg");
                response3.EnsureSuccessStatusCode();
                responseBytes = await response3.Content.ReadAsByteArrayAsync();
                Assert.Equal(contentRoot.GetResourceBytes("TestFiles.fire-umbrella-small.jpg"), responseBytes);
                await host.StopAsync(CancellationToken.None);
            }
        }
        
        [Fact]
        public async void TestDiskCache()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/logo.png", "TestFiles.imazen_400.png"))
            {

                var diskCacheDir = Path.Combine(contentRoot.PhysicalPath, "diskcache");
                var hostBuilder = new HostBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddImageflowDiskCache(
                            new DiskCacheOptions(diskCacheDir) {AsyncWrites = false});
                    })
                    .ConfigureWebHost(webHost =>
                    {
                        // Add TestServer
                        webHost.UseTestServer();
                        webHost.Configure(app =>
                        {
                            app.UseImageflow(new ImageflowMiddlewareOptions()
                                .SetMapWebRoot(false)
                                .SetAllowDiskCaching(true)
                                // Maps / to ContentRootPath/images
                                .MapPath("/", Path.Combine(contentRoot.PhysicalPath, "images")));
                        });
                    });

                // Build and start the IHost
                using var host = await hostBuilder.StartAsync();

                // Create an HttpClient to send requests to the TestServer
                using var client = host.GetTestClient();

                using var response = await client.GetAsync("/not_there.jpg");
                Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
                
                using var response2 = await client.GetAsync("/fire.jpg?width=1");
                response2.EnsureSuccessStatusCode();
                var responseBytes = await response2.Content.ReadAsByteArrayAsync();
                Assert.True(responseBytes.Length < 1000);
                
                using var response3 = await client.GetAsync("/fire.jpg");
                response3.EnsureSuccessStatusCode();
                responseBytes = await response3.Content.ReadAsByteArrayAsync();
                Assert.Equal(contentRoot.GetResourceBytes("TestFiles.fire-umbrella-small.jpg"), responseBytes);

                await host.StopAsync(CancellationToken.None);
                
                var cacheFiles = Directory.GetFiles(diskCacheDir, "*.jpg", SearchOption.AllDirectories);
                Assert.Single(cacheFiles);
            }
        }
        
         [Fact]
        public async void TestAmazonS3()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/logo.png", "TestFiles.imazen_400.png"))
            {

                var diskCacheDir = Path.Combine(contentRoot.PhysicalPath, "diskcache");
                var hostBuilder = new HostBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddImageflowDiskCache(new DiskCacheOptions(diskCacheDir) {AsyncWrites = false});
                        services.AddImageflowS3Service(
                            new S3ServiceOptions(null, null)
                                .MapPrefix("/ri/", RegionEndpoint.USEast1, "resizer-images"));
                    })
                    .ConfigureWebHost(webHost =>
                    {
                        // Add TestServer
                        webHost.UseTestServer();
                        webHost.Configure(app =>
                        {
                            app.UseImageflow(new ImageflowMiddlewareOptions()
                                .SetMapWebRoot(false)
                                .SetAllowDiskCaching(true)
                                // Maps / to ContentRootPath/images
                                .MapPath("/", Path.Combine(contentRoot.PhysicalPath, "images")));
                        });
                    });

                // Build and start the IHost
                using var host = await hostBuilder.StartAsync();

                // Create an HttpClient to send requests to the TestServer
                using var client = host.GetTestClient();

                using var response = await client.GetAsync("/ri/not_there.jpg");
                Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
                
                using var response2 = await client.GetAsync("/ri/imageflow-icon.png?width=1");
                response2.EnsureSuccessStatusCode();
                
                await host.StopAsync(CancellationToken.None);
                
                var cacheFiles = Directory.GetFiles(diskCacheDir, "*.png", SearchOption.AllDirectories);
                Assert.Single(cacheFiles);
            }
        }
    }
}