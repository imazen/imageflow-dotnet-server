using Imageflow.Server;
using Imazen.Abstractions.DependencyInjection;
using Imazen.Abstractions.Logging;
using Imazen.Routing.Promises.Pipelines.Watermarking;
using Imazen.Routing.Engine;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Layers;
using Imazen.Routing.Serving;
using Imazen.Tests.Routing.Serving;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Imazen.Routing.Tests.Serving;

using TContext = object;
public class ImageServerTests
{
    private readonly ILicenseChecker licenseChecker;
    private readonly IReLoggerFactory loggerFactory;
    private readonly IReLogger logger;
    private readonly IReLogStore logStore;
    private readonly List<MemoryLogEntry> logList;
    private IImageServerContainer container;

    public ImageServerTests()
    {
        licenseChecker = MockLicenseChecker.AlwaysOK();
        logList = new List<MemoryLogEntry>();
        logStore = new ReLogStore(new ReLogStoreOptions());
        loggerFactory = MockHelpers.MakeMemoryLoggerFactory(logList, logStore);
        logger = loggerFactory.CreateReLogger("ImageServerTests");
        container = new ImageServerContainer(null);
        container.Register<ILicenseChecker>(() => licenseChecker);
        container.Register<IReLoggerFactory>(() => loggerFactory);
        container.Register<IReLogStore>(() => logStore);
        container.Register<LicenseOptions>(() => new LicenseOptions
        {
            
            ProcessWideCandidateCacheFoldersDefault = new string[]
            {
                Path.GetTempPath() // Typical env.ContentRootPath as well
            }
        });

    }

    internal ImageServer<MockRequestAdapter, MockResponseAdapter, TContext>
        CreateImageServer(
            RoutingEngine routingEngine,
            ImageServerOptions imageServerOptions
        )
    {
        return new ImageServer<MockRequestAdapter, MockResponseAdapter, TContext>(
            container,
            container.GetRequiredService<ILicenseChecker>(),
            container.GetRequiredService<LicenseOptions>(),
            routingEngine,
            container.GetService<IPerformanceTracker>() ?? new NullPerformanceTracker(),
            logger
        );
    }

    [Fact]
    public void TestMightHandleRequest()
    {
        var b = new RoutingBuilder();
        b.AddEndpoint(Conditions.PathEquals("/hi"),
            SmallHttpResponse.Text(200, "HI"));
        
        var imageServer = CreateImageServer(
            b.Build(logger),
            new ImageServerOptions());
       
        
        Assert.True(imageServer.MightHandleRequest("/hi", DictionaryQueryWrapper.Empty, new TContext()));

    }

    [Fact]
    public async void TestTryHandleRequestAsync()
    {
        var b = new RoutingBuilder();
        b.AddEndpoint(Conditions.PathEquals("/hi"),
            SmallHttpResponse.Text(200, "HI"));
        
        var imageServer = CreateImageServer(
            b.Build(logger),
            new ImageServerOptions()
        );
        var request = MockRequest.GetLocalRequest("/hi").ToAdapter();
        var response = new MockResponseAdapter();
        var context = new TContext();
        var handled = await imageServer.TryHandleRequestAsync(request, response, context);
        Assert.True(handled);
        var r = await response.ToMockResponse();
        
        Assert.Equal(200, r.StatusCode);
        Assert.Equal("HI", r.DecodeBodyUtf8());
    }
}