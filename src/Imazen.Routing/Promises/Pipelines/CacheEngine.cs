using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Imazen.Abstractions;
using Imazen.Abstractions.BlobCache;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Logging;
using Imazen.Abstractions.Resulting;
using Imazen.Common.Concurrency;
using Imazen.Common.Concurrency.BoundedTaskCollection;
using Imazen.Routing.Helpers;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Requests;
using Microsoft.Extensions.Logging;

namespace Imazen.Routing.Promises.Pipelines;

/// <summary>
/// Optimized for quick startup, such as for serverless scenarios. No optimizations for multiple requests
/// </summary>
public class CacheEngine: IBlobPromisePipeline
{

    public CacheEngine(IBlobPromisePipeline? next, CacheEngineOptions options)
    {
        Options = options;
        Next = next;
        AllCachesCount = Options.SeriesOfCacheGroups.Sum(x => x.Count);
        if (options.LockByUniqueRequest)
        {
            Locks = new AsyncLockProvider();
        }
    }
    
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
        // TODO: we probably don't want to blob cache blobs that are of the same latency let alone blob cache local source files..
        // But we probably always want to check the memory cache?   
        
        
        return CodeResult<ICacheableBlobPromise>.Ok(
            new ServerlessCachePromise(wrappedPromise.FinalRequest, wrappedPromise, this));
    }

    private IBlobPromisePipeline? Next { get; }
    private AsyncLockProvider? Locks { get; }
    
    protected int AllCachesCount;
    
    public CacheEngineOptions Options { get; }
    
    public static IBlobCacheRequest For(ICacheableBlobPromise promise, BlobGroup blobGroup)
    {
        var bytes = promise.GetCacheKey32Bytes();
        var hex = bytes.ToHexLowercase();
        return new BlobCacheRequest(blobGroup, bytes, hex, false);
    }
    
    private async Task FinishUpload(IBlobCacheRequest cacheReq, ICacheableBlobPromise promise, BlobWrapper blob, CancellationToken cancellationToken = default)
    {
        var cacheEventDetails =  CacheEventDetails.CreateFreshResultGeneratedEvent(cacheReq, Options.BlobFactory, BlobCacheFetchFailure.OkResult(blob));
        await Task.WhenAll(Options.SaveToCaches.Select(x => x.CachePut(cacheEventDetails, cancellationToken)));
    }

    public async ValueTask<CodeResult<IBlobWrapper>> Fetch(ICacheableBlobPromise promise,IBlobRequestRouter router,  CancellationToken cancellationToken = default)
    {
        if (!promise.ReadyToWriteCacheKeyBasisData)
        {
            throw new InvalidOperationException("Caller should have resolved the dependencies already");
        }
        IBlobCacheRequest cacheRequest = For(promise, BlobGroup.GeneratedCacheEntry);
        
        if (Locks != null)
        {
            Options.Logger.LogDebug("Waiting for lock on {CacheKeyHashString}", cacheRequest.CacheKeyHashString);
            var result = await Locks.TryExecuteAsync(cacheRequest.CacheKeyHashString,
                Options.LockTimeoutMs, cancellationToken, ValueTuple.Create(cacheRequest, promise),
                async (v, ct) => await FetchInner(v.Item1, v.Item2, router, ct));
            
            var returns = result.IsError ? CodeResult<IBlobWrapper>.Err(HttpStatus.ServiceUnavailable.WithAddFrom("Timeout waiting for lock")) : result.Unwrap();
            Options.Logger.LogDebug("Lock on {CacheKeyHashString} released", cacheRequest.CacheKeyHashString);
            if (returns.IsError) Options.Logger.LogDebug("Error fetching {CacheKeyHashString}: {Error}", cacheRequest.CacheKeyHashString, returns);
            return returns;
        }
        else
        {
            return await FetchInner(cacheRequest, promise, router, cancellationToken);
        }
    }
    

    
    public async ValueTask<CodeResult<IBlobWrapper>> FetchInner(IBlobCacheRequest cacheRequest, ICacheableBlobPromise promise, IBlobRequestRouter router,  CancellationToken cancellationToken = default)
    {
        // First check the upload queue.
        if (Options.UploadQueue?.TryGet(cacheRequest.CacheKeyHashString, out var uploadTask) == true)
        {
            Options.Logger.LogTrace("Located requested resource from the upload queue {CacheKeyHashString}", cacheRequest.CacheKeyHashString);
            return CodeResult<IBlobWrapper>.Ok(uploadTask.Blob);
        }
        // Then check the caches
        List<KeyValuePair<IBlobCache,Task<CacheFetchResult>>>? allFetchAttempts = null;
        foreach (var parallelGroup in Options.SeriesOfCacheGroups)
        {
            if (parallelGroup.Count == 0) continue;
            if (parallelGroup.Count == 1)
            {
                Options.Logger.LogTrace("Checking {CacheName} for {Hash}",  parallelGroup[0].UniqueName, cacheRequest.CacheKeyHashString);
                var task = parallelGroup[0].CacheFetch(cacheRequest, cancellationToken);
                if (!(await task).TryUnwrap(out var blobWrapper))
                {
                    allFetchAttempts ??= new List<KeyValuePair<IBlobCache,Task<CacheFetchResult>>>(AllCachesCount);
                    allFetchAttempts.Add(new KeyValuePair<IBlobCache, Task<CacheFetchResult>>(parallelGroup[0], task));
                    continue;
                }
                // If there's another group, it might be relevant
                EnqueueSaveToCaches(cacheRequest, ref blobWrapper,false, parallelGroup[0], allFetchAttempts);
                return CodeResult<IBlobWrapper>.Ok(blobWrapper);
            }
            else
            {
                // Create a way to cancel the stragglers.
                var subCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                // create but don't await the tasks
                allFetchAttempts ??= new List<KeyValuePair<IBlobCache,Task<CacheFetchResult>>>(AllCachesCount);
                var cacheFetchTasks = new List<Task<CacheFetchResult>>(parallelGroup.Count);
                foreach (var cache in parallelGroup)
                {
                    Options.Logger.LogTrace("Checking {CacheName} for {Hash}",  cache.NameAndClass(), cacheRequest.CacheKeyHashString);
                    var t = cache.CacheFetch(cacheRequest, subCancel.Token);
                    cacheFetchTasks.Add(t);
                    allFetchAttempts.Add(new KeyValuePair<IBlobCache, Task<CacheFetchResult>>(cache, t));
                }
                
                // wait for the first one to succeed
                CacheFetchResult? firstSuccess = null;
                try
                {
                    firstSuccess =
                        await ConcurrencyHelpers.WhenAnyMatchesOrDefault(cacheFetchTasks, x => x.IsOk,
                            cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Our caller cancelled us via cancellationToken. subCancel should automatically be cancelling too. 
                    LogFetchTaskStatus(false, null, allFetchAttempts);
                    throw;
                }

                // cancel all cacheFetchTasks, they won't cancel if they are already done/canceled/succeeded
                subCancel.Cancel(); // We don't want to wait for them to cancel.
                
                // if we have a success, return it
                if (firstSuccess != null && firstSuccess.TryUnwrap(out var blobWrapper))
                {
                    EnqueueSaveToCaches(cacheRequest, ref blobWrapper, false, null, allFetchAttempts);
                    return CodeResult<IBlobWrapper>.Ok(blobWrapper);
                }
            }
        }

        var freshResult = await promise.TryGetBlobAsync(promise.FinalRequest, router, this, cancellationToken);
        if (freshResult.IsError) return freshResult;
        var blob = freshResult.Unwrap();
        EnqueueSaveToCaches(cacheRequest, ref blob,true, null, allFetchAttempts);
        return CodeResult<IBlobWrapper>.Ok(blob);
    }
    
    protected virtual bool ShouldSaveToCache(IBlobCache cache, bool isFresh, IBlobCacheFetchFailure? cacheResponse, [NotNullWhen(true)]out IBlobCache? cacheToSaveTo)
    {
        cacheToSaveTo = cache;
        if (cacheResponse == null) return isFresh || cache.InitialCacheCapabilities.SubscribesToExternalHits;
        switch (isFresh)
        {
            case false when cacheResponse.NotifyOfExternalHit != null:
                cacheToSaveTo = cacheResponse.NotifyOfExternalHit;
                return true;
            case true when cacheResponse.NotifyOfResult != null:
                cacheToSaveTo = cacheResponse.NotifyOfResult;
                return true;
        }
        return false;
    }

    private void LogFetchTaskStatus(bool isFresh,
        IBlobCache? cacheHit, List<KeyValuePair<IBlobCache, Task<CacheFetchResult>>>? fetchTasks)
    {
        if (Options.Logger.IsEnabled(LogLevel.Trace))
        {
            // list all fetch tasks, and their status [miss] [pending] [HIT]
            // then if we generated a fresh result
            if (fetchTasks == null)
            {
                if (cacheHit == null)
                {
                    Log.LogTrace("No caches were queried.");
                }
                else
                {
                    Log.LogTrace("[HIT] {CacheName} (first and only cache queried)", cacheHit.UniqueName);
                }
            }
            else
            {
                foreach (var pair in fetchTasks)
                {
                    var cache = pair.Key;
                    var task = pair.Value;
                    if (task is { Status: TaskStatus.RanToCompletion })
                    {
                        if (task.Result.TryUnwrapError(out var err))
                        {
                            if (err.Status == HttpStatus.NotFound)
                            {
                                Log.LogTrace("[miss] Cache {CacheName} returned 404", cache.NameAndClass());
                            }
                            else
                            {
                                Log.LogTrace("[error] Cache {CacheName} failed with {Error}", cache.NameAndClass(), err);
                            }
                            Log.LogTrace("[miss] Cache {CacheName} failed with {Error}", cache.NameAndClass(), err);
                        }
                        else
                        {
                            var attrs = task.Result.Unwrap().Attributes;
#pragma warning disable CS0618 // Type or member is obsolete
                            Log.LogTrace("[HIT] {CacheName} - returned {ContentType}, {ByteEstimate} bytes", cache.NameAndClass(), attrs.ContentType, attrs.BlobByteCount);
#pragma warning restore CS0618 // Type or member is obsolete
                        }
                    }
                    else
                    {
                        Log.LogTrace("[pending] Cache {CacheName} task status: {Status}", cache.NameAndClass(), task.Status);
                    }
                }
            }

            if (isFresh)
            {
                Log.LogTrace("Generated a fresh result");
            }
        }
    }

    /// <summary>
    /// Inheritors can further track cache health and limit the candidates
    /// </summary>
    /// <param name="isFresh"></param>
    /// <param name="cacheHit"></param>
    /// <param name="fetchTasks"></param>
    /// <returns></returns>
    protected List<IBlobCache>? GetUploadCacheCandidates(bool isFresh,
        ref IBlobCache? cacheHit, List<KeyValuePair<IBlobCache, Task<CacheFetchResult>>>? fetchTasks)
    {
        if (Log.IsEnabled(LogLevel.Trace))
        {
            LogFetchTaskStatus(isFresh, cacheHit, fetchTasks);
        }
        
        
        // Create the list of caches to save to. If cancelled before completion, we default to notifying it. Otherwise, we only upload to caches that resulted with a fetch failure.
        List<IBlobCache>? cachesToSaveTo = null; 
        foreach (var candidate in Options.SaveToCaches)
        {
            if (candidate == cacheHit) continue;
            var fetchTask = fetchTasks?.FirstOrDefault(x => x.Key == candidate).Value;
            if (fetchTask is { Status: TaskStatus.RanToCompletion }){
                if (fetchTask.Result.TryUnwrapError(out var err))
                {
                    if (ShouldSaveToCache(candidate, isFresh, err, out var cacheToSaveTo2))
                    {
                        cachesToSaveTo ??= new List<IBlobCache>(Options.SaveToCaches.Count);
                        cachesToSaveTo.AddIfUnique(cacheToSaveTo2);
                    }
                }
                else
                {
                    if (!isFresh && cacheHit == null)
                    {
                        cacheHit = candidate; // Determine the candidate that was hit during a parallel fetch
                    }
                }
            }
            else if (ShouldSaveToCache(candidate, isFresh, null, out var cacheToSaveTo))
            {
                cachesToSaveTo ??= new List<IBlobCache>(Options.SaveToCaches.Count);
                cachesToSaveTo.AddIfUnique(cacheToSaveTo);
            }
        }

        if (Options.Logger.IsEnabled(LogLevel.Debug))
        {
            // list the unique names of all caches we're saving to
            if (cachesToSaveTo == null)
            {
                Log.LogDebug("Uploading to 0 caches");
            }
            else
            {
                Log.LogDebug("Uploading to {Count} caches: {CacheNames}", cachesToSaveTo.Count, cachesToSaveTo.Select(x => x.UniqueName));
            }
        }
        
        return cachesToSaveTo;
    }
    
    private IReLogger Log => Options.Logger;
    
    private void EnqueueSaveToCaches(IBlobCacheRequest cacheRequest, ref IBlobWrapper blob, bool isFresh,
            IBlobCache? cacheHit, List<KeyValuePair<IBlobCache,Task<CacheFetchResult>>>? fetchTasks)
    {
        // if (Options.UploadQueue == null) return;
        
        var cachesToSaveTo = GetUploadCacheCandidates(isFresh, ref cacheHit, fetchTasks);
        
        if (cachesToSaveTo == null || Options.UploadQueue == null) return; // Nothing to do
        if (!blob.IsNativelyReusable) throw new InvalidOperationException("Blob must be natively reusable");

        CacheEventDetails? eventDetails = null;
        if (isFresh)
        {
            eventDetails = CacheEventDetails.CreateFreshResultGeneratedEvent(cacheRequest, Options.BlobFactory, BlobCacheFetchFailure.OkResult(blob));
        }
        else if (cacheHit != null)
        {
            eventDetails = CacheEventDetails.CreateExternalHitEvent(cacheRequest, cacheHit, Options.BlobFactory, BlobCacheFetchFailure.OkResult(blob));
        }
        else
        {
            //TODO: log a bug, we should be able to find cacheHit among the set 
            throw new InvalidOperationException();
        }
        
        Options.UploadQueue.Queue(new BlobTaskItem(cacheRequest.CacheKeyHashString,blob), async (taskItem, cancellationToken) =>
        {
            var tasks = cachesToSaveTo.Select(async cache => {
                    var sw = Stopwatch.StartNew();
                    try
                    {

                        var result = await cache.CachePut(eventDetails, cancellationToken);
                        sw.Stop();
                        return new PutResult(cache, eventDetails, result, null, sw.ElapsedMilliseconds);
                    }
                    catch (Exception e)
                    {
                        sw.Stop();
                        return new PutResult(cache, eventDetails, null, e, sw.ElapsedMilliseconds);
                    }
                }
            ).ToArray();
            HandleUploadAnswers(await Task.WhenAll(tasks));
        });
    }
    record struct PutResult(IBlobCache Cache, CacheEventDetails EventDetails, CodeResult? Result, Exception? Exception, long DurationMs);

    private void HandleUploadAnswers(PutResult[] results)
    {
        //TODO? 
// 1. If any cache failed, log it

    }


    
    
}

internal record ServerlessCachePromise(IRequestSnapshot FinalRequest, ICacheableBlobPromise FreshPromise, CacheEngine CacheEngine): ICacheableBlobPromise
{
    public bool IsCacheSupporting => true;
    public bool HasDependencies => FreshPromise.HasDependencies;
    public bool ReadyToWriteCacheKeyBasisData => FreshPromise.ReadyToWriteCacheKeyBasisData;
    public bool SupportsPreSignedUrls => FreshPromise.SupportsPreSignedUrls;

    public LatencyTrackingZone? LatencyZone => null;
    
    private byte[]? cacheKey32Bytes = null;
    public byte[] GetCacheKey32Bytes()
    {
        return cacheKey32Bytes ??= this.GetCacheKey32BytesUncached();
    }
    
    public ValueTask<CodeResult> RouteDependenciesAsync(IBlobRequestRouter router, CancellationToken cancellationToken = default)
    {
        return FreshPromise.RouteDependenciesAsync(router, cancellationToken);
    }

    public void WriteCacheKeyBasisPairsToRecursive(IBufferWriter<byte> writer)
    {
        FreshPromise.WriteCacheKeyBasisPairsToRecursive(writer);
    }

    public async ValueTask<IAdaptableHttpResponse> CreateResponseAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline, CancellationToken cancellationToken = default)
    {
        return new BlobResponse(await TryGetBlobAsync(request, router, pipeline, cancellationToken));
    }

    public async ValueTask<CodeResult<IBlobWrapper>> TryGetBlobAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline, CancellationToken cancellationToken = default)
    {
        if (!FreshPromise.ReadyToWriteCacheKeyBasisData)
        {
            var res = await FreshPromise.RouteDependenciesAsync(router, cancellationToken);
            if (res.TryUnwrapError(out var err))
            {
                // pass through 404
                if (err.StatusCode == HttpStatus.NotFound) return CodeResult<IBlobWrapper>.Err(err);
                
                // throw the others for now
                throw new InvalidOperationException("Promise has dependencies but could not be routed: " + err);
            }
            
        }

        return await CacheEngine.Fetch(FreshPromise, router, cancellationToken);
    }
}
  