using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Imageflow.Fluent;
using Imageflow.Server.DiskCache;
using Imageflow.Server.Extensibility;
using Imageflow.Server.SqliteCache;
using Imageflow.Server.Storage.S3;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Imageflow.Server.Tests
{
    public class SqliteTest
    {

     
        [Fact]
        public async void TestSqliteCache()
        {
            using var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg")
                .AddResource("images/logo.png", "TestFiles.imazen_400.png");
            var hostBuilder = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddImageflowSqliteCache(
                        new SqliteCacheOptions(":memory:"));
                })
                .ConfigureWebHost(webHost =>
                {
                    // Add TestServer
                    webHost.UseTestServer();
                    webHost.Configure(app =>
                    {
                        app.UseImageflow(new ImageflowMiddlewareOptions()
                            .SetMapWebRoot(false)
                            .SetAllowSqliteCaching(true)
                            // Maps / to ContentRootPath/images
                            .MapPath("/", Path.Combine(contentRoot.PhysicalPath, "images")));
                    });
                });

            // Build and start the IHost
            using var host = await hostBuilder.StartAsync();

            // Create an HttpClient to send requests to the TestServer
            using var client = host.GetTestClient();

            for (var i = 0; i < 5; i++)
            {
                for (var j = 0; j < 5; j++)
                {
                    using var response2 = await client.GetAsync($"/fire.jpg?width={i}");
                    response2.EnsureSuccessStatusCode();
                }
            }

            await host.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async void LoadTestSqliteCache()
        {
            var cache = new SqliteCacheService(new SqliteCacheOptions(":memory:"),null);

            await cache.StartAsync(CancellationToken.None);
            var tasks = new List<Task>();
            for (var i = 0; i < 100; i++)
            {
                tasks.Add(
                    Task.Run(async () =>
                    {
                        for (var j = 0; j < 100; j++)
                        {
                            await Task.Delay(10);
                            var result = await cache.GetOrCreate($"{j}", async () =>
                            {
                                await Task.Delay(200);
                                return new SqliteCacheEntry()
                                {
                                    ContentType = "",
                                    Data = new byte[100]
                                };
                            });
                        }
                    }));
            }
            

            await Task.WhenAll(tasks);
            
            await cache.StopAsync(CancellationToken.None);
        }
     
    }
}