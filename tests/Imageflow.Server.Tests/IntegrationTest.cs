using System;
using System.Collections.Generic;
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
                .AddResource("images/fire.jfif", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/fire umbrella.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/logo.png", "TestFiles.imazen_400.png")
                .AddResource("images/wrong.webp", "TestFiles.imazen_400.png")
                .AddResource("images/wrong.jpg", "TestFiles.imazen_400.png"))
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
                                .MapPath("/insensitive", Path.Combine(contentRoot.PhysicalPath, "images"), true)
                                .MapPath("/sensitive", Path.Combine(contentRoot.PhysicalPath, "images"), false)
                                .AddWatermark(new NamedWatermark("imazen", "/logo.png", new WatermarkOptions()))
                                .AddWatermark(new NamedWatermark("broken", "/not_there.png", new WatermarkOptions()))
                                .AddWatermarkingHandler("/", args =>
                                {
                                    if (args.Query.TryGetValue("water", out var value) && value == "mark")
                                    {
                                        args.AppliedWatermarks.Add(new NamedWatermark(null, "/logo.png", new WatermarkOptions()));
                                    }
                                }));
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

                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    using var watermarkInvalidResponse = await client.GetAsync("/fire.jpg?watermark=not-a-watermark");
                });
                
                using var watermarkResponse = await client.GetAsync("/fire.jpg?watermark=imazen");
                watermarkResponse.EnsureSuccessStatusCode();
                
                using var watermarkResponse2 = await client.GetAsync("/fire.jpg?water=mark");
                watermarkResponse2.EnsureSuccessStatusCode();

                using var wrongImageExtension1 = await client.GetAsync("/wrong.webp");
                wrongImageExtension1.EnsureSuccessStatusCode();
                Assert.Equal("image/png", wrongImageExtension1.Content.Headers.ContentType.MediaType);
                
                using var wrongImageExtension2 = await client.GetAsync("/wrong.jpg");
                wrongImageExtension2.EnsureSuccessStatusCode();
                Assert.Equal("image/png", wrongImageExtension2.Content.Headers.ContentType.MediaType);

                
                
                using var response2 = await client.GetAsync("/fire.jpg?width=1");
                response2.EnsureSuccessStatusCode();
                var responseBytes = await response2.Content.ReadAsByteArrayAsync();
                Assert.True(responseBytes.Length < 1000);
                
                using var response3 = await client.GetAsync("/fire%20umbrella.jpg");
                response3.EnsureSuccessStatusCode();
                responseBytes = await response3.Content.ReadAsByteArrayAsync();
                Assert.Equal(contentRoot.GetResourceBytes("TestFiles.fire-umbrella-small.jpg"), responseBytes);
                
                using var response4 = await client.GetAsync("/inSenSitive/fire.jpg?width=1");
                response4.EnsureSuccessStatusCode();
                
                
                
                using var response5 = await client.GetAsync("/senSitive/fire.jpg?width=1");
                Assert.Equal(HttpStatusCode.NotFound, response5.StatusCode);
                
                using var response6 = await client.GetAsync("/sensitive/fire.jpg?width=1");
                response6.EnsureSuccessStatusCode();
                
                using var response7 = await client.GetAsync("/fire.jfif?width=1");
                response7.EnsureSuccessStatusCode();
                var responseBytes7 = await response7.Content.ReadAsByteArrayAsync();
                Assert.True(responseBytes7.Length < 1000);
                
                
                await host.StopAsync(CancellationToken.None);
            }
        }
        
        [Fact]
        public async void TestDiskCache()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/logo.png", "TestFiles.imazen_400.png")
                .AddResource("images/wrong.webp", "TestFiles.imazen_400.png")
                .AddResource("images/wrong.jpg", "TestFiles.imazen_400.png"))
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

                using var wrongImageExtension1 = await client.GetAsync("/wrong.webp");
                wrongImageExtension1.EnsureSuccessStatusCode();
                Assert.Equal("image/png", wrongImageExtension1.Content.Headers.ContentType.MediaType);
                
                using var wrongImageExtension2 = await client.GetAsync("/wrong.jpg");
                wrongImageExtension2.EnsureSuccessStatusCode();
                Assert.Equal("image/png", wrongImageExtension2.Content.Headers.ContentType.MediaType);


                
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
        
        [Fact]
        public async void TestPresetsExclusive()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg"))
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
                                .SetUsePresetsExclusively(true)
                                .AddPreset(new PresetOptions("small", PresetPriority.OverrideQuery)
                                    .SetCommand("maxwidth", "1")
                                    .SetCommand("maxheight", "1"))
                                );
                        });
                    });

                // Build and start the IHost
                using var host = await hostBuilder.StartAsync();

                // Create an HttpClient to send requests to the TestServer
                using var client = host.GetTestClient();

                using var notFoundResponse = await client.GetAsync("/not_there.jpg");
                Assert.Equal(HttpStatusCode.NotFound,notFoundResponse.StatusCode);
                
                using var foundResponse = await client.GetAsync("/fire.jpg");
                foundResponse.EnsureSuccessStatusCode();
                
                
                using var presetValidResponse = await client.GetAsync("/fire.jpg?preset=small");
                presetValidResponse.EnsureSuccessStatusCode();
                
                
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    using var watermarkInvalidResponse = await client.GetAsync("/fire.jpg?preset=not-a-preset");
                });
                
                using var nonPresetResponse = await client.GetAsync("/fire.jpg?width=1");
                Assert.Equal(HttpStatusCode.Forbidden,nonPresetResponse.StatusCode);
                
                await host.StopAsync(CancellationToken.None);
            }
        }

        [Fact]
        public async void TestPresets()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg"))
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
                                .AddPreset(new PresetOptions("tiny", PresetPriority.OverrideQuery)
                                    .SetCommand("width", "2")
                                    .SetCommand("height", "1"))
                                .AddPreset(new PresetOptions("small", PresetPriority.DefaultValues)
                                    .SetCommand("width", "30")
                                    .SetCommand("height", "20"))
                                );
                        });
                    });

                // Build and start the IHost
                using var host = await hostBuilder.StartAsync();

                // Create an HttpClient to send requests to the TestServer
                using var client = host.GetTestClient();

                using var presetValidResponse = await client.GetAsync("/fire.jpg?preset=small&height=35&mode=pad");
                presetValidResponse.EnsureSuccessStatusCode();
                var responseBytes = await presetValidResponse.Content.ReadAsByteArrayAsync();
                var imageResults = await ImageJob.GetImageInfo(new BytesSource(responseBytes));
                Assert.Equal(30,imageResults.ImageWidth);
                Assert.Equal(35,imageResults.ImageHeight);
                
                
                using var presetTinyResponse = await client.GetAsync("/fire.jpg?preset=tiny&height=35");
                presetTinyResponse.EnsureSuccessStatusCode();
                responseBytes = await presetTinyResponse.Content.ReadAsByteArrayAsync();
                imageResults = await ImageJob.GetImageInfo(new BytesSource(responseBytes));
                Assert.Equal(2,imageResults.ImageWidth);
                Assert.Equal(1,imageResults.ImageHeight);
                
                await host.StopAsync(CancellationToken.None);
            }
        }
        
         [Fact]
        public async void TestRequestSigning()
        {
            const string key = "test key";
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire umbrella.jpg", "TestFiles.fire-umbrella-small.jpg"))
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
                                .SetRequireRequestSignature(true)
                                .AddRequestSigningKey(key)
                                );
                        });
                    });
                using var host = await hostBuilder.StartAsync();
                using var client = host.GetTestClient();
                
                using var unsignedResponse = await client.GetAsync("/fire umbrella.jpg?width=1");
                Assert.Equal(HttpStatusCode.Forbidden,unsignedResponse.StatusCode);

                var signedUrl = Imazen.Common.Helpers.Signatures.SignRequest("/fire umbrella.jpg?width=1", key);
                using var signedResponse = await client.GetAsync(signedUrl);
                signedResponse.EnsureSuccessStatusCode();
                
                var signedEncodedUnmodifiedUrl = Imazen.Common.Helpers.Signatures.SignRequest("/fire%20umbrella.jpg", key);
                using var signedEncodedUnmodifiedResponse = await client.GetAsync(signedEncodedUnmodifiedUrl);
                signedEncodedUnmodifiedResponse.EnsureSuccessStatusCode();
                
                
                var signedEncodedUrl = Imazen.Common.Helpers.Signatures.SignRequest("/fire%20umbrella.jpg?width=1", key);
                using var signedEncodedResponse = await client.GetAsync(signedEncodedUrl);
                signedEncodedResponse.EnsureSuccessStatusCode();
                
                var url5 = Imazen.Common.Helpers.Signatures.SignRequest("/fire umbrella.jpg?width=1&ke%20y=val%2fue&another key=another val/ue", key);
                using var response5 = await client.GetAsync(url5);
                response5.EnsureSuccessStatusCode();
                
                await host.StopAsync(CancellationToken.None);
            }
        }
    }
}