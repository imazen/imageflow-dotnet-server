using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Instrumentation;
using Imazen.Common.Instrumentation.Support.InfoAccumulators;
using Imazen.Common.Licensing;
using Imazen.Common.Storage;
using Imazen.Abstractions.BlobCache;
using Imageflow.Server.Internal;
using Imageflow.Server.LegacyOptions;
using Imazen.Abstractions.Blobs.LegacyProviders;
using Imazen.Abstractions.DependencyInjection;
using Imazen.Abstractions.Logging;
using Imazen.Routing.Health;
using Imazen.Routing.Serving;
using Microsoft.Extensions.DependencyInjection;

namespace Imageflow.Server
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ImageflowMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ImageflowMiddlewareOptions options;
        private readonly GlobalInfoProvider globalInfoProvider;
        private readonly IImageServer<RequestStreamAdapter,ResponseStreamAdapter, HttpContext>  imageServer;
        public ImageflowMiddleware(
            RequestDelegate next, 
            IWebHostEnvironment env, 
            IServiceProvider serviceProvider, // You can request IEnumerable<T> to get all of them.
            IEnumerable<IReLoggerFactory> loggerFactories, ILoggerFactory legacyLoggerFactory,
            IEnumerable<IReLogStore> retainedLogStores,
#pragma warning disable CS0618 // Type or member is obsolete
            IEnumerable<IClassicDiskCache> diskCaches, 
            IEnumerable<IStreamCache> streamCaches, 
            IEnumerable<IBlobProvider> blobProviders, 
#pragma warning restore CS0618 // Type or member is obsolete
            IEnumerable<IBlobWrapperProvider> blobWrapperProviders,
            IEnumerable<IBlobCache> blobCaches, 
            IEnumerable<IBlobCacheProvider> blobCacheProviders, 
            ImageflowMiddlewareOptions options)
        {
            
            var retainedLogStore = retainedLogStores.FirstOrDefault() ?? new ReLogStore(new ReLogStoreOptions());
            var loggerFactory = loggerFactories.FirstOrDefault() ?? new ReLoggerFactory(legacyLoggerFactory, retainedLogStore);
            var logger = loggerFactory.CreateReLogger("ImageflowMiddleware");

            this.next = next;
            this.options = options;

            var container = new ImageServerContainer(serviceProvider);

            globalInfoProvider = new GlobalInfoProvider(container);
            container.Register(globalInfoProvider);
            
      
            
            container.Register(env);
            container.Register(logger);
            
           
   
            new MiddlewareOptionsServerBuilder(container, logger, retainedLogStore, options,env).PopulateServices();
            
            
            var startDiag = new StartupDiagnostics(container);
            startDiag.LogIssues(logger);
            startDiag.Validate(logger);
            
            imageServer = container.GetRequiredService<IImageServer<RequestStreamAdapter,ResponseStreamAdapter, HttpContext>>();
        }

        private bool hasPopulatedHttpContextExample = false;
        public async Task Invoke(HttpContext context)
        {
            
            var queryWrapper = new QueryCollectionWrapper(context.Request.Query);
            // We can optimize for the path where we know we won't be handling the request
            if (!imageServer.MightHandleRequest(context.Request.Path.Value, queryWrapper, context))
            {
                await next.Invoke(context);
                return;
            }
            // If we likely will be handling it, we can allocate the shims
            var requestAdapter = new RequestStreamAdapter(context.Request);
            var responseAdapter = new ResponseStreamAdapter(context.Response);
            
            //For instrumentation
            if (!hasPopulatedHttpContextExample){
                hasPopulatedHttpContextExample = true;
                globalInfoProvider.CopyHttpContextInfo(requestAdapter);
            }
            
            if (await imageServer.TryHandleRequestAsync(requestAdapter, responseAdapter, context, context.RequestAborted))
            {
                return; // We handled it
            }
            await next.Invoke(context);
 
        }
    }
}
