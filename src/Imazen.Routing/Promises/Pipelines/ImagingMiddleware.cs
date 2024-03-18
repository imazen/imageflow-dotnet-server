using System.Buffers;
using System.Diagnostics;
using System.Text;
using Imageflow.Fluent;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.HttpStrings;
using Imazen.Abstractions.Logging;
using Imazen.Abstractions.Resulting;
using Imazen.Routing.Promises.Pipelines.Watermarking;
using Imazen.Routing.Helpers;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Requests;
using Microsoft.Extensions.Primitives;

namespace Imazen.Routing.Promises.Pipelines;

// TODO: SemaphoreSlim for concurrency limits, with self-monitoring and adjusting perhaps?
// And likely some method of backpressure prior to large allocations. We probably want a max concurrency limit and a max waiters limit
// and if the max waiters limit is hit, we can wait *prior* to fetching sources.


//
// if (licenseChecker.RequestNeedsEnforcementAction(request))
// {
//     if (this.legacyOptions.EnforcementMethod == EnforceLicenseWith.RedDotWatermark)
//     {
//         FinalQuery["watermark_red_dot"] = "true";
//     }
//     LicenseError = true;
// }

public record ImagingMiddlewareOptions
{
    public required IReLogger Logger { get; init; }
    
    public required IReusableBlobFactory BlobFactory { get; init; }
    
    public required WatermarkingLogicOptions WatermarkingLogic { get; init; }
    
    
    internal SecurityOptions JobSecurityOptions { get; init; } = new SecurityOptions();
    
    
    // Concurrency limits
    
    // Outsourcing the job to other servers.
    // it would be best if they could PUT directly to the output cache
    // but this middleware has no knowledge of that. 
    // and sometimes they might reply directly with the image result
    
    // we also have preview versions to consider
    
    // and watermarking handlers.
    
    // and chained versions, like intermediate conversions etc.
}


public record ImagingMiddleware(IBlobPromisePipeline? Next, ImagingMiddlewareOptions Options) : IBlobPromisePipeline
{
    
    public async ValueTask<CodeResult<ICacheableBlobPromise>> GetFinalPromiseAsync(ICacheableBlobPromise promise, IBlobRequestRouter router,
        IBlobPromisePipeline promisePipeline, IHttpRequestStreamAdapter outerRequest, CancellationToken cancellationToken = default)
    {
        var wrappedPromise = promise;
        if (Next != null)
        {
            var result = await Next.GetFinalPromiseAsync(promise, router, promisePipeline, outerRequest, cancellationToken);
            if (result.IsError) return result;
            wrappedPromise = result.Unwrap();
        }
        
        // Check for watermarking work to do
        var appliedWatermarks = Options.WatermarkingLogic.GetAppliedWatermarks(promise.FinalRequest);
        if (appliedWatermarks?.Count == 0)
        {
            // We need to route the watermarking dependencies
            appliedWatermarks = null;
        }

        // Filter to the supported querystring keys
        Dictionary<string, StringValues>? commandDict = null;
        var dict = promise.FinalRequest.QueryString;
        if (dict != null && dict.Count > 0)
        {
            foreach (var supportedKey in PathHelpers.SupportedQuerystringKeys)
            {
                if (!dict.TryGetValue(supportedKey, out var value)) continue;
                commandDict ??= new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                commandDict[supportedKey] = value;
            }
        }

        if (commandDict == null && appliedWatermarks == null)
        {
            // No image processing to do...
            return CodeResult<ICacheableBlobPromise>.Ok(wrappedPromise);
        }
        var finalCommandString = commandDict == null ? "" : QueryHelpers.AddQueryString("", commandDict);
        return CodeResult<ICacheableBlobPromise>.Ok(
            new ImagingPromise(Options,wrappedPromise, router, promisePipeline, appliedWatermarks, finalCommandString, outerRequest));
    }
}


internal record ImagingPromise : ICacheableBlobPromise
{
    public ImagingPromise(ImagingMiddlewareOptions options, 
        ICacheableBlobPromise sourcePromise, IBlobRequestRouter router,
        IBlobPromisePipeline promisePipeline, IList<WatermarkWithPath>? appliedWatermarks, 
        string finalCommandString, IHttpRequestStreamAdapter outerRequest)
        
    {
        Input0 = sourcePromise;
        RequestRouter = router;
        PromisePipeline = promisePipeline;
        Options = options;
        AppliedWatermarks = appliedWatermarks;
        FinalCommandString = finalCommandString.TrimStart('?');
        
        // determine if we have extra dependencies
        
        if (AppliedWatermarks != null)
        {
            ExtraDependencyRequests = new List<MutableRequest>(AppliedWatermarks.Count);
            foreach (var watermark in AppliedWatermarks)
            {
                //TODO: flaw in the system, we can't get the original request adapter from the snapshot!
                // should never be null
                var request = MutableRequest.ChildRequest(outerRequest, sourcePromise.FinalRequest, 
                    watermark.VirtualPath, "GET");
                ExtraDependencyRequests.Add(request);
            }
        }
        if (ExtraDependencyRequests == null)
        {
            Dependencies = new List<ICacheableBlobPromise>(){Input0};
        }
        
        
    }
    private byte[]? cacheKey32Bytes = null;
    public byte[] GetCacheKey32Bytes()
    {
        return cacheKey32Bytes ??= this.GetCacheKey32BytesUncached();
    }
    
    private LatencyTrackingZone? latencyZone = null;
    /// <summary>
    /// Must route dependencies first!
    /// </summary>
    public LatencyTrackingZone? LatencyZone {
        get
        {
            if (!ReadyToWriteCacheKeyBasisData) throw new InvalidOperationException("Dependencies must be routed first");
            // produce a latency zone based on all dependency strings, joined, plus the sum of their latency defaults
            if (latencyZone != null) return latencyZone;
            var latency = 0;
            var sb = new StringBuilder();
            sb.Append("imageJob(");
            foreach (var dependency in Dependencies!)
            {
                latency += dependency.LatencyZone?.DefaultMs ?? 0;
                sb.Append(dependency.LatencyZone?.TrackingZone ?? "(unknown)");
            }
            sb.Append(")");
            latencyZone = new LatencyTrackingZone(sb.ToString(), latency, true);
            return latencyZone;
        }
    }
    

    private string FinalCommandString { get; init; }

    public async ValueTask<CodeResult<IBlobWrapper>> TryGetBlobAsync(IRequestSnapshot request,
        IBlobRequestRouter router,
        IBlobPromisePipeline pipeline, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        if (Dependencies == null) throw new InvalidOperationException("Dependencies must be routed first");
        
        var toDispose = new List<IDisposable>(Dependencies.Count);
        
        try{
            // fetch all dependencies in parallel, but avoid allocating if there's only one.
            List<CodeResult<IConsumableMemoryBlob>> dependencyResults = new List<CodeResult<IConsumableMemoryBlob>>(Dependencies.Count);
            if (Dependencies.Count == 1)
            {
                var fetch1 = await (await Dependencies[0]
                    .TryGetBlobAsync(Dependencies[0].FinalRequest, router, pipeline, cancellationToken))
                    .MapOkAsync(async wrapper =>
                    {
                        var mem = await wrapper.GetConsumableMemoryPromise().IntoConsumableMemoryBlob();
                        wrapper.Dispose();
                        return mem;
                    })
                    .ConfigureAwait(false);
                dependencyResults.Add(fetch1);
                if (fetch1.TryUnwrap(out var unwrapped))
                {
                    toDispose.Add(unwrapped);
                }
            }
            else
            {
                // TODO: work on exception bubbling
                var fetchTasks = new Task<CodeResult<IConsumableMemoryBlob>>[Dependencies.Count];
                for (var i = 0; i < Dependencies.Count; i++)
                {
                    var dep = Dependencies[i];
                    // TODO: fix
                    fetchTasks[i] = Task.Run(async () =>
                    {
                        var result = await dep.TryGetBlobAsync(dep.FinalRequest, router, pipeline, cancellationToken);
                        var finalResult = await result.MapOkAsync(async wrapper =>
                        {
                            var mem = await wrapper.GetConsumableMemoryPromise().IntoConsumableMemoryBlob();
                            wrapper.Dispose();
                            return mem;
                        });
                        return finalResult;
                    });
                }

                try
                {
                    await Task.WhenAll(fetchTasks);
                }
                finally
                {   
                    // Collect everything that needs to be disposed
                    // Some may have completed and have pending connections or unmanaged resources
                    // So if there was an error we need to dispose whatever is still open
                    foreach (var task in fetchTasks)
                    {
                        if (task.Status != TaskStatus.RanToCompletion) continue;
                        if (task.Result.TryUnwrap(out var unwrapped))
                        {
                            toDispose.Add(unwrapped);
                        }
                    }
                }
                foreach (var task in fetchTasks)
                {
                    dependencyResults.Add(task.Result);
                }
            }
            // Check for errors and create the sources

            var byteSources = new List<IAsyncMemorySource>(dependencyResults.Count);
            foreach (var result in dependencyResults)
            {
      
                // TODO: aggregate them!
                // and maybe specify it was a watermark or a source image
                if (result.TryUnwrap(out var unwrapped))
                {
                    byteSources.Add(MemorySource.Borrow(unwrapped.BorrowMemory, MemoryLifetimePromise.MemoryValidUntilAfterJobDisposed));
                }
                else
                {
                    return CodeResult<IBlobWrapper>.Err(result.UnwrapError());
                }
            }

            List<InputWatermark>? watermarks = null;
            if (AppliedWatermarks != null)
            {
                watermarks = new List<InputWatermark>(AppliedWatermarks.Count);
                watermarks.AddRange(
                    AppliedWatermarks.Select((t, i) 
                        => new InputWatermark(
                            byteSources[i + 1],
                            t.Watermark)));
            }

            
            using var buildJob = new ImageJob();
            var jobResult = 
                await (watermarks == null ? buildJob.BuildCommandString(
                    byteSources[0],
                    new BytesDestination(), FinalCommandString)
                : buildJob.BuildCommandString(
                    byteSources[0],
                    new BytesDestination(), FinalCommandString, watermarks))
                
                .Finish()
                .SetSecurityOptions(Options.JobSecurityOptions)
                .InProcessAsync();
            
            // remove all 

            // TODO: restore instrumentation
            // GlobalPerf.Singleton.JobComplete(new ImageJobInstrumentation(jobResult)
            // {
            //     FinalCommandKeys = FinalQuery.Keys,
            //     ImageDomain = ImageDomain,
            //     PageDomain = PageDomain
            // });

            // TryGetBytes returns the buffer from a regular MemoryStream, not a recycled one
            var encodeResult = jobResult.First ?? throw new InvalidOperationException("Image job did not return a resulting image (no encode result)");
            var resultBytes = encodeResult.TryGetBytes();
            if (!resultBytes.HasValue || resultBytes.Value.Count < 1 || resultBytes.Value.Array == null)
            {
                throw new InvalidOperationException("Image job returned zero bytes.");
            }

            var attrs = new BlobAttributes()
            {
                LastModifiedDateUtc = DateTime.UtcNow,
                ContentType = encodeResult.PreferredMimeType,
                BlobByteCount = resultBytes.Value.Count
            };
            sw.Stop();
            var reusable = new MemoryBlob(resultBytes.Value, attrs, sw.Elapsed);
            
            
            var processedResult =  CodeResult<IBlobWrapper>.Ok(new BlobWrapper(LatencyZone, reusable));
            return processedResult;
        }
        finally
        {
            foreach (var b in toDispose)
            {
                b?.Dispose();
            }
        }

    }

    private IList<WatermarkWithPath>? AppliedWatermarks { get; init; }

    private ImagingMiddlewareOptions Options { get; init; }
    private IBlobRequestRouter RequestRouter { get; init; }
    private IBlobPromisePipeline PromisePipeline { get; init; }
    private ICacheableBlobPromise Input0 { get; init; }

    private List<MutableRequest>? ExtraDependencyRequests { get; init; }
    private List<ICacheableBlobPromise>? Dependencies { get; set; }




    public bool IsCacheSupporting => Input0.IsCacheSupporting;
    public IRequestSnapshot FinalRequest => Input0.FinalRequest;

    public async ValueTask<IAdaptableHttpResponse> CreateResponseAsync(IRequestSnapshot request, IBlobRequestRouter router,
        IBlobPromisePipeline pipeline,
        CancellationToken cancellationToken = default)
    {
        return new BlobResponse(await TryGetBlobAsync(request, router, pipeline, cancellationToken));
    }

    public bool SupportsPreSignedUrls => false; // not yet

    public bool HasDependencies =>
        Input0.HasDependencies || ExtraDependencyRequests == null || ExtraDependencyRequests?.Count > 0;

    public bool ReadyToWriteCacheKeyBasisData => Dependencies != null;

    public async ValueTask<CodeResult> RouteDependenciesAsync(IBlobRequestRouter router,
        CancellationToken cancellationToken = default)
    {
        if (Dependencies != null)
        {
            return CodeResult.Ok();
        }

        Dependencies = new List<ICacheableBlobPromise>((ExtraDependencyRequests?.Count ?? 0) + 1) { Input0 };
        if (ExtraDependencyRequests != null)
        {
            HttpStatus aggregate = default;
            foreach (var request in ExtraDependencyRequests)
            {
                // This needs to be robust, actually, it happens a lot.
                var result = await router.RouteToPromiseAsync(request, cancellationToken)
                             ?? CodeResult<ICacheableBlobPromise>.Err((404, $"Could not find dependency {request}"));
                if (result.TryUnwrap(out var unwrapped))
                {
                    Dependencies.Add(unwrapped);
                }
                else
                {
                    // Pass through if there's only one error, otherwise aggregate them into a single HttpStatus
                    var error = result.UnwrapError();
                    aggregate = aggregate == default(HttpStatus) ? error : aggregate.WithAppend($" and {error}");
                }
            }

            if (aggregate != default(HttpStatus))
            {
                return CodeResult.Err(aggregate);
            }
        }

        // Now call recursively on each dependency
        foreach (var dependency in Dependencies)
        {
            if (!dependency.HasDependencies) continue;
            var result = await dependency.RouteDependenciesAsync(router, cancellationToken);
            if (result.IsError) return result;
        }

        return CodeResult.Ok();
    }

    public void WriteCacheKeyBasisPairsToRecursive(IBufferWriter<byte> writer)
    {
        if (Dependencies == null) throw new InvalidOperationException("Dependencies must be routed first");
        foreach (var dependency in Dependencies)
        {
            dependency.WriteCacheKeyBasisPairsToRecursive(writer);
        }

        writer.WriteWtf(FinalCommandString);
        
        // Write watermarking cache key basis
        WatermarkingLogicOptions.WriteWatermarkingCacheKeyBasis(AppliedWatermarks, writer);
    }

  
    public bool TryGeneratePreSignedUrl(IRequestSnapshot request, out string? url) =>
        throw new NotImplementedException();

}