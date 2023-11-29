# Other issues
ReLogStore diagnostic section report
Multi-tenant support across shared IBlobCache - blob prefixes.
Ensure clarity in whether routes will only handle image extension requests, +extensionless, or +arbitrary or +certain. CORS and mime type definitions are needed.
Diagnostics: check nuget for latest version of imageflow*
Skip signing requirement for dependencies, like watermarks?
Create IExternalService interface for Lambda/Functions/Replicate/other Imageflow JSON job builder
Establish /build.job json endpoint

Establish IBlobStore interface, or convert IBlobCache interface into IBlobStore. It should support 4 use cases at minimum
1. Get/put blobs at a given path for user use, and list blobs within a given path (s3 directory vs bucket may add considerations)
2. Support the functions needed for IBlobCache
3. Get/put blobs for usage as a job status tracker (such as if the request has been queued with a third party, what the status is, and where the results are at)
4. Pre-signed URLs for upload/download are essential
5. Manage underlying path length and character limitations with encoding/and or hashing as needed; IBlobStore defines the limits and our system works around them.
Establish disk, s3, and azure implementations of IBlobStore.
All disk code should be aware that it could be on-network and be high-latency friendly. 


# ImagingMiddleware design

## TODO: Intermediate variant chaining
where we have a dependency on an intermediate variant. We could resolve the promise, determine its existence probability,
and then decide if we want to use it as input. If we do, we can use that promise as input instead. If not, we can use the original input.
We need to spawn a job if it is missing, however, so future calls can use it.

## TODO: header->query rewriting: Accept-Type: image/webp;q=0.9,image/jpeg;q=0.8,image/png;q=0.7,image/*;q=0.6,*/*;q=0.5 

# CacheEngine design


## TODO: add ability to mark some requests as top-priority for in-memory (such as watermarks)
## TODO: establish both serverless-optimized and long-running optimized CacheEngineOptions variants

## TODO: Permacaching mode, where nothing gets invalidated? 


## Test cache searching, purging, and deletion, and auditing everywhere something is stored

## ExistenceProbability could be reported instantly with the promise...

## Allow phasing in global invalidations / config changes


## Optimzizing the fetch/put sets (other than just caching everything with AlwaysShield=true)
    // TODO: track and report fetch times for each cache
    // Do a bit of randomization within capabilities so we don't avoid collecting data. For these randoms, let all fetch results complete.
    // We also let the first (n) cache fetches complete when starting up, so we have data. 
    // We always ignore the first result from any Zone
    
    // We can make decisions based on the fresh promise zone VS the cache zone
    // BlobWrapper returns the cache zone, the created date, and (when made reusable) the duration of the buffering process.
    // But we need to externally track which IBlobCache instances return which BlobWrappers to properly gather intel
    
    // All middleware consumes BlobWrappers, so every middleware will need to instrument it.. but since all IBlobCache
    // interaction is here, only we need to handle IBlobCache mapping...
    // Theoretically multiple caches could use the same zone/bucket. So a single zone could have multiple IBlobCaches
    // which is fine since we just want to know the zone perf for each IBlobCache
    
    
    // We need to gather monitored TTFB/TTLB/byte data for each cache.
    // When we have a fresh result, if it's from a blob provider, we can monitor that data too
    // If it is from 'imaging', that middleware needs to monitor the blob results...
    // Choose a cache strategy based on promise latency vs cache latency
    
    // And of course, if a promise has AlwaysShield=true, we cache it regardless.
    // TODO: see what we can steal from instrumentation in licensing for sampling & stats


CacheEngine can be configured for serverless or long-running use. 
In serverless mode, we rely more heavily on defaults and less on monitoring-based tuning.



============ Old stuff


TrustedKeyGroups can allow public/private signing so that the public keys can be deployed to the edge, and the private keys can be used to sign requests.


### Put hard logic in BlobCacheExecutor

* AsyncWriteQueue
* MemoryCache
* RecentlyCachedHashes (tracking where we've recently cached stuff)
* ExistenceProbability (tracking how likely a cache is to have a file)
* CircuitBreaker (tracking if a cache is failing to read or write)
* Parallel cache requests.
* CircuitBreaker failures may mean initializing a new cache (such as a hybrid cache to a backup location) and adding it to the chain.
* 

We are abandoning the callback system, because it limits us and makes caches complex.
We are obsoleting StreamCache.

## Some paths

* A cache is hit, but a later cache doesn't have a chance at the request. It needs an ExternalHit notificaiton
* A cache experiences a miss, but another cache has a hit. ExternalHitYouMissed
* A cache experiences a miss, and no other cache has a hit. ExternalMiss
* ExternalMiss and successful generation of the image. ResultGenerated
* ExternalMiss and failure to generate the image. ResultFailed
* Proxied request. ResultProxiedSuccessfully


-----
What we need to do is switch away from the callback system. While chaining is an option, what's really happening with callbacks is enabling locking for work deduplication. 

When a cache has a miss, it also returns a request to be notified of when data is available for that request.  Caches get notifications when data is generated (or located in another cache) for something they missed - but also for when a request is hit via another cache earlier in the chain.

If all caches miss, the engine locks on the image generation process. 
When we succeed through the lock, we check the async write queue (which will now be shared among all caches). If it's a hit there, then we can respond using that reusable stream to serve the data. 

If it's not in the async write queue, we could either (a) current behavior for hybridcache - check the cache storage again) or (b) check a 'recently cached hashes log which is reset every few minutes to determine if we should re-run the cache fetch, or (c) not check caches, just generate the image. (c) assumes that writes will take enough time to complete that the images will always stay in the queue a little longer (and we could sleep 50ms or something to help that), or (d) check the write queue, then check the recent puts log, if present, then fetch from cache.


I think this approach, a central write queue and external cache coordination, offers the most flexibility and is the way to go, even though it means breaking the streamcache API. 

Now, for requests that do not involve any file processing - proxy requests. For these, creating a reusable stream involves extra costs and we want to avoid it. To reduce proxy latency, we should copy to the output stream simultaneously with a memory stream (maybe?) and then enqueue that write as normal. 

We also want to support a no-cache scenario efficiently - why s3 cache s3 files, right? But even then, some buffering is probably good

We probably want a generalized memory cache, bounded, possibly not eagerly evicted, and one that can clear entries that failed their tasks or timed out. We want some insight into it as well, such as turnover and cache hit rate. 

To reduce fragmentation, we probably want a memory chunk provider that can reuse blocks. We would have to refcount reader streams before releasing, however.

Dual framework imazen.common perhaps.

We should probably utilize circuit breaker on cache reads and writes. And take latency stats for fetches and writes, perhaps to determine cache order?

For hybridcache, we can add a look-in-alt-cache directories setting, or just circuit breaker cache writes independently from cache reads, and just create (n) caches. We could lockfile the whole write side of the cache too, and leave that memory index offline for it.


Fetches, writes, and tag searches should all return location references for audit/logging 

And for cache fetches, we probably want *some* caches to fetch in parallel and take whomever comes back first. 

So instead of a chain, we want a list of groups?











