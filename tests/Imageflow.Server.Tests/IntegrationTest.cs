using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Imageflow.Fluent;
using Imageflow.Server.HybridCache;
using Imageflow.Server.Storage.RemoteReader;
using Imageflow.Server.Storage.S3;
using Imazen.Abstractions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Imageflow.Server.Tests
{
    public static class LoggingExtensions
    {
        internal static void AddXunitLoggingDefaults(this IServiceCollection services, ITestOutputHelper outputHelper)
        {
            services.AddLogging(builder =>
            {
                // log to output so we get it on xunit failures.
                builder.AddXUnit(outputHelper, configuration =>
                {
                    configuration.Filter = (category, level) => level >= LogLevel.Trace;
                   
                });
                builder.SetMinimumLevel(LogLevel.Trace);
                // builder.AddFilter("Microsoft", LogLevel.Warning);
                // builder.AddFilter("System", LogLevel.Warning);
                // trace log to console for when xunit crashes with stack overflow
                builder.AddConsole(options =>
                {
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                });
            });
            services.AddImageflowReLogStoreAndReLoggerFactoryIfMissing();
        }
    }
    public class IntegrationTest(ITestOutputHelper outputHelper)
    {

        
        [Fact]
        public async void TestLocalFiles()
        {
            using (var contentRoot = new TempContentRoot(outputHelper)
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/fire.jfif", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/fire umbrella.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/logo.png", "TestFiles.imazen_400.png")
                .AddResource("images/wrong.webp", "TestFiles.imazen_400.png")
                .AddResource("images/wrong.jpg", "TestFiles.imazen_400.png")
                .AddResource("images/extensionless/file", "TestFiles.imazen_400.png"))
            {
                var hostBuilder = new HostBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddXunitLoggingDefaults(outputHelper);
                    })
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
                                .HandleExtensionlessRequestsUnder("/extensionless/")
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
                Assert.Equal("image/png", wrongImageExtension1.Content.Headers.ContentType?.MediaType);
                
                using var wrongImageExtension2 = await client.GetAsync("/wrong.jpg");
                wrongImageExtension2.EnsureSuccessStatusCode();
                Assert.Equal("image/png", wrongImageExtension2.Content.Headers.ContentType?.MediaType);

                using var extensionlessRequest = await client.GetAsync("/extensionless/file");
                extensionlessRequest.EnsureSuccessStatusCode();
                Assert.Equal("image/png", extensionlessRequest.Content.Headers.ContentType?.MediaType);

                
                using var response2 = await client.GetAsync("/fire.jpg?width=1");
                response2.EnsureSuccessStatusCode();
                var responseBytes = await response2.Content.ReadAsByteArrayAsync();
                Assert.True(responseBytes.Length < 1000);
                
                using var response3 = await client.GetAsync("/fire umbrella.jpg"); //Works with space...
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
                
                using var response8 = await client.GetAsync("/imageflow.health");
                response8.EnsureSuccessStatusCode();
                using var response9 = await client.GetAsync("/imageflow.ready");
                response9.EnsureSuccessStatusCode();
                
                using var response10 = await client.GetAsync("/fire%20umbrella.jpg"); //Works with space...
                response10.EnsureSuccessStatusCode();
                
                await host.StopAsync(CancellationToken.None);
            }
        }

        [Fact]
        public async void TestDiskCache()
        {
            using var contentRoot = new TempContentRoot(outputHelper)
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/logo.png", "TestFiles.imazen_400.png")
                .AddResource("images/wrong.webp", "TestFiles.imazen_400.png")
                .AddResource("images/wrong.jpg", "TestFiles.imazen_400.png")
                .AddResource("images/extensionless/file", "TestFiles.imazen_400.png");


            var diskCacheDir = Path.Combine(contentRoot.PhysicalPath, "diskcache");
            await using var host = await new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddXunitLoggingDefaults(outputHelper);
                    services.AddImageflowHybridCache(
                        new HybridCacheOptions(diskCacheDir)); //TODO: sync writes wanted
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
                            .HandleExtensionlessRequestsUnder("/extensionless/")
                            // Maps / to ContentRootPath/images
                            .MapPath("/", Path.Combine(contentRoot.PhysicalPath, "images")));
                    });
                }).StartDisposableHost();

            var logger = host.Services.GetService<IReLoggerFactory>()!.CreateReLogger("test");

            logger.LogTrace("Test log");
            {
                // Create an HttpClient to send requests to the TestServer
                using var client = host.GetTestClient();

                using var response = await client.GetAsync("/not_there.jpg");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                using var response2 = await client.GetAsync("/fire.jpg?width=1");
                response2.EnsureSuccessStatusCode();
                var responseBytes = await response2.Content.ReadAsByteArrayAsync();
                Assert.True(responseBytes.Length < 1000);

                using (var response3 = await client.GetAsync("/fire.jpg"))
                {
                    response3.EnsureSuccessStatusCode();
                    responseBytes = await response3.Content.ReadAsByteArrayAsync();
                    Assert.Equal(contentRoot.GetResourceBytes("TestFiles.fire-umbrella-small.jpg"), responseBytes);
                }

                {
                    using var wrongImageExtension1 = await client.GetAsync("/wrong.webp");
                    wrongImageExtension1.EnsureSuccessStatusCode();
                    Assert.Equal("image/png", wrongImageExtension1.Content.Headers.ContentType?.MediaType);
                }

                using var wrongImageExtension2 = await client.GetAsync("/wrong.jpg");
                wrongImageExtension2.EnsureSuccessStatusCode();
                Assert.Equal("image/png", wrongImageExtension2.Content.Headers.ContentType?.MediaType);

                using var extensionlessRequest = await client.GetAsync("/extensionless/file");
                extensionlessRequest.EnsureSuccessStatusCode();
                Assert.Equal("image/png", extensionlessRequest.Content.Headers.ContentType?.MediaType);
            }
            await host.StopAsync(CancellationToken.None);
            await host.DisposeAsync();

            var cacheFiles = Directory.GetFiles(diskCacheDir, "*.jpg", SearchOption.AllDirectories);
            Assert.Single(cacheFiles);
        }

        [Fact]
        public async void TestAmazonS3()
        {
            using var contentRoot = new TempContentRoot(outputHelper)
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/logo.png", "TestFiles.imazen_400.png");


            var diskCacheDir = Path.Combine(contentRoot.PhysicalPath, "diskcache");
            await using var host = await new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddXunitLoggingDefaults(outputHelper);
                    services.AddSingleton<IAmazonS3>(new AmazonS3Client(new AnonymousAWSCredentials(),
                        RegionEndpoint.USEast1));
                    services.AddImageflowHybridCache(new HybridCacheOptions(diskCacheDir));
                    services.AddImageflowS3Service(
                        new S3ServiceOptions()
                            .MapPrefix("/ri/", "resizer-images"));

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
                }).StartDisposableHost();

            // Create an HttpClient to send requests to the TestServer
            using var client = host.GetTestClient();

            using var response = await client.GetAsync("/ri/not_there.jpg");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            using var response2 = await client.GetAsync("/ri/imageflow-icon.png?width=1");
            response2.EnsureSuccessStatusCode();

            await host.StopAsync(CancellationToken.None);

            // This could be failing because writes are still in the queue, or because no caches are deemed worthy of writing to, or health status reasons
            // TODO: diagnose 

            var cacheFiles = Directory.GetFiles(diskCacheDir, "*.jpg", SearchOption.AllDirectories);
            Assert.Single(cacheFiles);

        }

        [Fact]
        public async void TestAmazonS3WithCustomClient()
        {
            using (var contentRoot = new TempContentRoot(outputHelper)
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/logo.png", "TestFiles.imazen_400.png"))
            {

                var diskCacheDir = Path.Combine(contentRoot.PhysicalPath, "diskcache");
                var hostBuilder = new HostBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddXunitLoggingDefaults(outputHelper);
                        services.AddSingleton<IAmazonS3>(new AmazonS3Client(new AnonymousAWSCredentials(), RegionEndpoint.USEast1));
                        services.AddImageflowHybridCache(new HybridCacheOptions(diskCacheDir));
                        services.AddImageflowS3Service(
                            new S3ServiceOptions()
                                .MapPrefix("/ri/", new AmazonS3Client(new AnonymousAWSCredentials(), RegionEndpoint.USEast1), "resizer-images", "", false, false));
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
                
                var cacheFiles = Directory.GetFiles(diskCacheDir, "*.jpg", SearchOption.AllDirectories);
                Assert.Single(cacheFiles);
            }
        }


        [Fact]
        public async void TestPresetsExclusive()
        {
            using var contentRoot = new TempContentRoot(outputHelper)
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg");
            await using var host = await new HostBuilder()
                .ConfigureServices(services => { services.AddXunitLoggingDefaults(outputHelper); })
                .ConfigureWebHost(webHost =>
                {
                    // Add TestServer
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddImageflowHybridCache(
                            new HybridCacheOptions(Path.Combine(contentRoot.PhysicalPath, "diskcache")));
                        services.AddXunitLoggingDefaults(outputHelper);
                    });
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
                }).StartDisposableHost();
            
            // Create an HttpClient to send requests to the TestServer
            using var client = host.GetTestClient();

            using var notFoundResponse = await client.GetAsync("/not_there.jpg");
            Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);

            using var foundResponse = await client.GetAsync("/fire.jpg");
            foundResponse.EnsureSuccessStatusCode();


            using var presetValidResponse = await client.GetAsync("/fire.jpg?preset=small");
            presetValidResponse.EnsureSuccessStatusCode();


            using var watermarkInvalidResponse = await client.GetAsync("/fire.jpg?preset=not-a-preset");
            Assert.Equal(HttpStatusCode.BadRequest,watermarkInvalidResponse.StatusCode);
            
            using var nonPresetResponse = await client.GetAsync("/fire.jpg?width=1");
            Assert.Equal(HttpStatusCode.Forbidden, nonPresetResponse.StatusCode);
        }

        [Fact]
        public async void TestPresets()
        {
            using (var contentRoot = new TempContentRoot(outputHelper)
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg"))
            {

                var hostBuilder = new HostBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddXunitLoggingDefaults(outputHelper);
                    })
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
                var imageResults = await ImageJob.GetImageInfoAsync(new MemorySource(responseBytes), SourceLifetime.NowOwnedAndDisposedByTask);
                Assert.Equal(30,imageResults.ImageWidth);
                Assert.Equal(35,imageResults.ImageHeight);
                
                
                using var presetTinyResponse = await client.GetAsync("/fire.jpg?preset=tiny&height=35");
                presetTinyResponse.EnsureSuccessStatusCode();
                responseBytes = await presetTinyResponse.Content.ReadAsByteArrayAsync();
                imageResults = await ImageJob.GetImageInfoAsync(new MemorySource(responseBytes), SourceLifetime.NowOwnedAndDisposedByTask);
                Assert.Equal(2,imageResults.ImageWidth);
                Assert.Equal(1,imageResults.ImageHeight);
                
                await host.StopAsync(CancellationToken.None);
            }
        }
        
         [Fact]
        public async void TestRequestSigning()
        {
            const string key = "test key";
            using (var contentRoot = new TempContentRoot(outputHelper)
                .AddResource("images/fire umbrella.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/query/umbrella.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/never/umbrella.jpg", "TestFiles.fire-umbrella-small.jpg"))
            {

                var hostBuilder = new HostBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddXunitLoggingDefaults(outputHelper);
                    })
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
                                .SetRequestSignatureOptions(
                                    new RequestSignatureOptions(SignatureRequired.ForAllRequests, 
                                            new []{key})
                                        .ForPrefix("/query/", StringComparison.Ordinal, 
                                            SignatureRequired.ForQuerystringRequests, new []{key})
                                        .ForPrefix("/never/", StringComparison.Ordinal, SignatureRequired.Never,
                                            new string[]{}))
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

                var unsignedUnmodifiedUrl = "/query/umbrella.jpg";
                using var unsignedUnmodifiedResponse = await client.GetAsync(unsignedUnmodifiedUrl);
                unsignedUnmodifiedResponse.EnsureSuccessStatusCode();
                
                using var unsignedResponse2 = await client.GetAsync("/query/umbrella.jpg?width=1");
                Assert.Equal(HttpStatusCode.Forbidden,unsignedResponse2.StatusCode);

                var unsignedUnmodifiedUrl2 = "/never/umbrella.jpg";
                using var unsignedUnmodifiedResponse2 = await client.GetAsync(unsignedUnmodifiedUrl2);
                unsignedUnmodifiedResponse2.EnsureSuccessStatusCode();
                
                var unsignedModifiedUrl = "/never/umbrella.jpg?width=1";
                using var unsignedModifiedResponse = await client.GetAsync(unsignedModifiedUrl);
                unsignedModifiedResponse.EnsureSuccessStatusCode();
                
                var signedEncodedUrl = Imazen.Common.Helpers.Signatures.SignRequest("/fire%20umbrella.jpg?width=1", key);
                using var signedEncodedResponse = await client.GetAsync(signedEncodedUrl);
                signedEncodedResponse.EnsureSuccessStatusCode();
                
                var url5 = Imazen.Common.Helpers.Signatures.SignRequest("/fire umbrella.jpg?width=1&ke%20y=val%2fue&another key=another val/ue", key);
                using var response5 = await client.GetAsync(url5);
                response5.EnsureSuccessStatusCode();
                
                await host.StopAsync(CancellationToken.None);
            }
        }
        
        [Fact]
        public async void TestRemoteReaderPlusRequestSigning()
        {
            // This is the key we use to encode the remote URL and ensure that we are authorized to fetch the given url
            const string remoteReaderKey = "remoteReaderSigningKey_changeMe";
            // This is the key we use to ensure that the set of modifications to the remote file is permitted.
            const string requestSigningKey = "test key";
            using (var contentRoot = new TempContentRoot(outputHelper)
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg"))
            {

                var hostBuilder = new HostBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddXunitLoggingDefaults(outputHelper);
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddHttpClient();
                        services.AddImageflowRemoteReaderService(new RemoteReaderServiceOptions()
                            {
                                SigningKey = remoteReaderKey
                            }.AddPrefix("/remote")
                        );
                    })
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
                                .SetRequestSignatureOptions(
                                    new RequestSignatureOptions(SignatureRequired.ForAllRequests, 
                                            new []{requestSigningKey})
                                ));
                        });
                    });
                using var host = await hostBuilder.StartAsync();
                using var client = host.GetTestClient();

                // The origin file
                var remoteUrl = "https://imageflow-resources.s3-us-west-2.amazonaws.com/test_inputs/imazen_400.png";
                // We encode it, but this doesn't add the /remote/ prefix since that is configurable
                var encodedRemoteUrl = RemoteReaderService.EncodeAndSignUrl(remoteUrl, remoteReaderKey);
                // Now we add the /remote/ prefix and add some commands
                var modifiedUrl = $"/remote/{encodedRemoteUrl}?width=1";
                
                
                // Now we could stop here, but we also enabled request signing which is different from remote reader signing
                var signedModifiedUrl = Imazen.Common.Helpers.Signatures.SignRequest(modifiedUrl, requestSigningKey);
                using var signedResponse = await client.GetAsync(signedModifiedUrl);
                signedResponse.EnsureSuccessStatusCode();
                
                // Now, verify that the remote url can't be fetched without signing it the second time, 
                // since we called .SetRequestSignatureOptions
                using var halfSignedResponse = await client.GetAsync(modifiedUrl);
                Assert.Equal(HttpStatusCode.Forbidden, halfSignedResponse.StatusCode);

                
                
                await host.StopAsync(CancellationToken.None);
            }
        }
    }
}