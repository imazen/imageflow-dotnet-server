using System.Text;
using Imageflow.Server;
using Imazen.Abstractions.BlobCache;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Blobs.LegacyProviders;
using Imazen.Abstractions.DependencyInjection;
using Imazen.Abstractions.Logging;
using Imazen.Abstractions.Resulting;
using Imazen.Common.Concurrency.BoundedTaskCollection;
using Imazen.Common.Instrumentation;
using Imazen.Common.Instrumentation.Support.InfoAccumulators;
using Imazen.Common.Licensing;
using Imazen.Routing.Caching;
using Imazen.Routing.Engine;
using Imazen.Routing.Helpers;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Layers;
using Imazen.Routing.Promises;
using Imazen.Routing.Promises.Pipelines;
using Imazen.Routing.Promises.Pipelines.Watermarking;
using Imazen.Routing.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Imazen.Routing.Serving;

internal class ImageServer<TRequest, TResponse, TContext> : IImageServer<TRequest,TResponse, TContext>, IHostedService
    where TRequest : IHttpRequestStreamAdapter 
    where TResponse : IHttpResponseStreamAdapter
{
    private readonly IReLogger logger;
    private readonly ILicenseChecker licenseChecker;
    private readonly RoutingEngine routingEngine;
    private readonly IBlobPromisePipeline pipeline;
    private readonly IPerformanceTracker perf;
    private readonly CancellationTokenSource cts = new();
    private readonly BoundedTaskCollection<BlobTaskItem> uploadQueue;
    private readonly bool shutdownRegisteredServices;
    private readonly IImageServerContainer container;
    public ImageServer(IImageServerContainer container,  
        LicenseOptions licenseOptions,
        RoutingEngine routingEngine, 
        IPerformanceTracker perfTracker,
        IReLogger logger, 
        bool shutdownRegisteredServices = true)
    {
        this.shutdownRegisteredServices = shutdownRegisteredServices;
        perf = perfTracker;
        this.container = container;
        this.logger = logger.WithSubcategory("ImageServer");
        this.routingEngine = routingEngine;

        licenseChecker = container.GetService<ILicenseChecker>() ??
                         new Licensing(LicenseManagerSingleton.GetOrCreateSingleton(
                             licenseOptions.KeyPrefix, licenseOptions.CandidateCacheFolders), null);
        licenseChecker.Initialize(licenseOptions);
                     
        licenseChecker.FireHeartbeat();
        var infoProviders = container.GetService<IEnumerable<IInfoProvider>>()?.ToList();
        if (infoProviders != null)
            GlobalPerf.Singleton.SetInfoProviders(infoProviders);
        
        
        var blobFactory = new SimpleReusableBlobFactory();
        
        // ensure routingengine is registered
        if (!container.Contains<RoutingEngine>())
            container.Register(routingEngine);
        
        uploadQueue = container.GetService<BoundedTaskCollection<BlobTaskItem>>();
        if (uploadQueue == null)
        {
            uploadQueue = new BoundedTaskCollection<BlobTaskItem>(1, cts);
            container.Register<BoundedTaskCollection<BlobTaskItem>>(uploadQueue);
        }

        var memoryCache = container.GetService<MemoryCache>();
        if (memoryCache == null)
        {
            memoryCache = new MemoryCache(new MemoryCacheOptions(
                "memCache", 100, 
                1000, 1000 * 10, TimeSpan.FromSeconds(10)));
            container.Register<MemoryCache>(memoryCache);
        }
        var allCaches = container.GetService<IEnumerable<IBlobCache>>()?.ToList();
        var allCachesExceptMemory = allCaches?.Where(c => c != memoryCache)?.ToList();

        var watermarkingLogic = container.GetService<WatermarkingLogicOptions>() ??
                                new WatermarkingLogicOptions(null, null);
        var sourceCacheOptions = new CacheEngineOptions
        {
            SeriesOfCacheGroups =
            [
                ..new[] { [memoryCache], allCachesExceptMemory ?? [] }
            ],
            SaveToCaches = allCaches!,
            BlobFactory = blobFactory,
            UploadQueue = uploadQueue,
            Logger = logger
        };
        var imagingOptions = new ImagingMiddlewareOptions
        {
            Logger = logger,
            BlobFactory = blobFactory,
            WatermarkingLogic = watermarkingLogic
        };

        pipeline = new CacheEngine(null, sourceCacheOptions);
        pipeline = new ImagingMiddleware(null, imagingOptions);
        pipeline = new CacheEngine(pipeline, sourceCacheOptions);
    }

    public string GetDiagnosticsPageSection(DiagnosticsPageArea area)
    {
        if (area != DiagnosticsPageArea.Start)
        {
            return "";
        }
        var s = new StringBuilder();
        s.AppendLine("\nInstalled Caches");
        s.AppendLine("\nInstalled Providers and Caches");
        // imageServer.GetInstalledProvidersDiag();
        return s.ToString();
    }

    public bool MightHandleRequest<TQ>(string? path, TQ query, TContext context) where TQ : IReadOnlyQueryWrapper
    {
        if (path == null) return false;
        return routingEngine.MightHandleRequest(path, query);
    }
    
    private ValueTask WriteHttpStatusErrAsync(TResponse response, HttpStatus error, CancellationToken cancellationToken)
    {
        perf.IncrementCounter($"http_{error.StatusCode}");
        return SmallHttpResponse.NoStoreNoRobots(error).WriteAsync(response, cancellationToken);
    }
    
    private string CreateEtag(ICacheableBlobPromise promise)
    {
        if (!promise.ReadyToWriteCacheKeyBasisData)
        {
            throw new InvalidOperationException("Promise is not ready to write cache key basis data");
        }
        var weakEtag = promise.CopyCacheKeyBytesTo(stackalloc byte[32])
            .ToHexLowercaseWith("W\"".AsSpan(), "\"".AsSpan());
        return weakEtag;
    }
    public async ValueTask<bool> TryHandleRequestAsync(TRequest request, TResponse response, TContext context, CancellationToken cancellationToken = default)
    {
        licenseChecker?.FireHeartbeat(); // Perhaps limit this to imageflow-handled requests?
        try
        {

            var mutableRequest = MutableRequest.OriginalRequest(request);
            var result = await routingEngine.Route(mutableRequest, cancellationToken);
            if (result == null)
                return false; // We don't have matching routing for this. Let the rest of the app handle it.
            if (result.IsError)
            {
                await WriteHttpStatusErrAsync(response, result.UnwrapError(), cancellationToken);
                return true;
            }

            var snapshot = mutableRequest.ToSnapshot(true);
            var endpoint = result.Unwrap();
            var promise = await endpoint.GetInstantPromise(snapshot, cancellationToken);
            if (promise is ICacheableBlobPromise blobPromise)
            {
                var pipelineResult =
                    await pipeline.GetFinalPromiseAsync(blobPromise, routingEngine, pipeline, request, cancellationToken);
                if (pipelineResult.IsError)
                {
                    await WriteHttpStatusErrAsync(response, pipelineResult.UnwrapError(), cancellationToken);
                    return true;
                }
                
                var finalPromise = pipelineResult.Unwrap();
                
                if (finalPromise.HasDependencies)
                {
                    var dependencyResult = await finalPromise.RouteDependenciesAsync(routingEngine, cancellationToken);
                    if (dependencyResult.IsError)
                    {
                        await WriteHttpStatusErrAsync(response, dependencyResult.UnwrapError(), cancellationToken);
                        return true;
                    }
                }
                
                string? promisedEtag = null;
                // Check for If-None-Match
                if (request.TryGetHeader(HttpHeaderNames.IfNoneMatch, out var conditionalEtag))
                {
                    promisedEtag = CreateEtag(finalPromise);
                    if (promisedEtag == conditionalEtag)
                    {
                        perf.IncrementCounter("etag_hit");
                        response.SetContentLength(0);
                        response.SetStatusCode(304);
                        return true;
                    }

                    perf.IncrementCounter("etag_miss");
                }

                // Now, let's get the actual blob. 
                var blobResult =
                    await finalPromise.TryGetBlobAsync(snapshot, routingEngine, pipeline, cancellationToken);
                if (blobResult.IsError)
                {
                    await WriteHttpStatusErrAsync(response, blobResult.UnwrapError(), cancellationToken);
                    return true;
                }

                var blob = blobResult.Unwrap();

                // TODO: if the blob provided an etag, it could be from blob storage, or it could be from a cache.
                // TODO: TryGetBlobAsync already calculates the cache if it's a serverless promise...
                // Since cache provider has to calculate the cache key anyway, can't we figure out how to improve this?
                promisedEtag ??= CreateEtag(finalPromise);

                if (blob.Attributes.Etag != null && blob.Attributes.Etag != promisedEtag)
                {
                    perf.IncrementCounter("etag_internal_external_mismatch");
                }

                response.SetHeader(HttpHeaderNames.ETag, promisedEtag);

                // TODO: Do routing layers configure this stuff? Totally haven't thought about it.
                //   if (options.DefaultCacheControlString != null)
                //       response.SetHeader(HttpHeaderNames.CacheControl, options.DefaultCacheControlString);

                if (blob.Attributes.ContentType != null)
                {
                    response.SetContentType(blob.Attributes.ContentType);
                    using var consumable = blob.MakeOrTakeConsumable();
                    await response.WriteBlobWrapperBody(consumable, cancellationToken);
                }
                else
                {
                    using var consumable = blob.MakeOrTakeConsumable();
                    using var stream = consumable.BorrowStream(DisposalPromise.CallerDisposesStreamThenBlob);
                    await MagicBytes.ProxyToStream(stream, response, cancellationToken);
                }

                perf.IncrementCounter("middleware_ok");
                return true;
            }
            else
            {
                var nonBlobResponse =
                    await promise.CreateResponseAsync(snapshot, routingEngine, pipeline, cancellationToken);
                await nonBlobResponse.WriteAsync(response, cancellationToken);
                perf.IncrementCounter("middleware_ok");
                return true;
            }



            
        }
        catch (BlobMissingException e)
        {
            perf.IncrementCounter("http_404");
            await SmallHttpResponse.Text(404, "The specified resource does not exist.\r\n" + e.Message)
                .WriteAsync(response, cancellationToken);
            return true;

        }
        catch (Exception e)
        {
            var errorName = e.GetType().Name;
            var errorCounter = "middleware_" + errorName;
            perf.IncrementCounter(errorCounter);
            perf.IncrementCounter("middleware_errors");
            throw;
        }
        finally
        {
            // Increment counter for type of file served
            var imageExtension = PathHelpers.GetImageExtensionFromContentType(response.ContentType);
            if (imageExtension != null)
            {
                perf.IncrementCounter("module_response_ext_" + imageExtension);
            }
        }

        throw new NotImplementedException("Unreachable");
        
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return uploadQueue.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        //TODO: error handling or no?
        cts.Cancel();
        await uploadQueue.StopAsync(cancellationToken);
        if (shutdownRegisteredServices)
        {
            var services = this.container.GetInstanceOfEverythingLocal<IHostedService>();
            foreach (var service in services)
            {
                await service.StopAsync(cancellationToken);
            }
        }
    }
}