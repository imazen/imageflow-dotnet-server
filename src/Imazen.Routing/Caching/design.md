# design notes (all of this is outdated and should be ignored)

We want to enable chaining, where one request could be based on another.

# Execution plan

A simple plan might just include the input image locations and 
the image job parameters.  
But what if we want to dramatically speed up operations on large images by detecting if we can use a smaller image variant as input, leveraging the caches? 

We could map a &preshrinkvariant=true parameter to behavior that uses the cache for a smaller variant. But what if the original is already small? 



# Cache Manager

Optional bits:
- circuit breaker functionality
- cache invalidation
- cache purging
- cache revalidation
- existence probability enhancement per-cache
- optimization based on real-time cache latency and time-to-last-byte
- optimization based on usage frequency (some caches might only want to be written to after x number of reads for the resource)
- cache failure paths, not just 200s. 404s, 502s, 403s, etc need a bit of caching, even if only for a few seconds.
- memory cache, not just a write queue. Can also track usage to determine
- if a write is justified..
- immutable mode vs mutable mode?






Unsolved - we may want to clear a cache of everything past a certain age, such as when we invalidate all data by changing a global image resize command, for example. But we may also want to allow a soft/slow invalidation process since that can slam a server. Most likely that would look like a 'phase-in-configuration' that is applied to requests when CPU utilization is below a certain threshold, and takes into account if a cache entry exists for the prior config. 



## For Lambda/fn/AOT
We want to make features with start-up latency optional. 

Such as: 
* Existence probable map
* Revalidation bit set
* AWS config loading (more a plugin thing)
* Large allocations / pools

Avoid slow Gettemppathname
consider Frozen collections
records classes/structs should be usable from .net classic
check for proxy perf tricks in https://microsoft.github.io/reverse-proxy/

## Normal content

The fresh content generation middleware, when called, will have a reference to the manager that accepts virtual paths. The manager will handle source file fetching/caching. We may consider more than just virtual paths as part of source file requests.


## (Complete) Key cache and provider interface needs:

Reusability and thread-safe buffering, so that a result can be sent to the requesting client AND stored in an upload queue for caching and (until then) memory caching/access.

But if no caching is happening, then we don't need to be able to open multiple read streams to the data, just one to proxy to the client. This is commom with requests for original files and blended asset type file serving. (BlobWrapper does this)

Each cache layer might at different times have different native capabilities, such as reading from http, file, or bytes, and different overhead for creating a reusable stream, so that buffering/upgrade should happen only on an as-needed basis. Metadata should always be immutable and thread-safe.

Cache keys should have both string and byte representation available in the key struct, and 'compliance tags' should also be stored and accessible - and searchable for adjusting access control or deletion. These could also be used for invalidation.

For reusable result objects, we want to be able to release the byte buffer losts back into the pool at some point, but this gets tricky with multiple underlying streams attached, unless all those streams are custom-designed to coordinate with the pool.


## Invalidation ideas

When we do a purge on a cache, we can extract the cache hashes, and mark those in the bit map for revalidation. Otherwise 304s will continue to be send for if-none-match, even if we are successful on the complete purge. Whatever the longest cache-string is for, we can theoretically cycle the bit maps. Ex, if it is one month - We write all bits to 2 different sets. We clear one set every month, alternating.

Note: Dual-purposing blobstoragereference doesn't give us an audit trail, just this one cache hit. Although technically that is the data source.

## 
Bit arrays based on hashes - false negatives only if (a) unexpected shutdown or (b) badly timed writes during persistence.

We would need an API for etag compareexchange stye cache puts... not hard (cache put options)

But also, this doesn't solve the source file invalidation problem, unless we track sources and check their invalidation hashes.

What we can do is create an execution plan - rapidly - for each incoming request, and then check all the input files against the probabilistic invalidation set.

We could also do a late-failure invalidation, such as checking the invalidation set for hashes of tags based on the tags provided by a cache backend. We probably need some time-based logic here, such as a date stamp of when the invalidation occurred, and a date stamp of when the cache entry was last created or invalidated.


# Bloom invalidation thoughts

Bit sets that exceed a certain threshold of density can trigger the creation of a new layer to the bloom filter. But we also want to protect from spammed invalidation commands. if we find an invalidation bit set, and the next ~4 are also set, then we should fall back to some kind of system that can tolerate mass invalidations rather than continuing to set bits. The problem is that encoding that data requires something smarter than ORing the bits, for eventual consistency.

We could require invalidation requesters specify the precise etag they want to invalidate (although they probably will hate that). OR, we could schedule a revalidation that fetches all the source files for a URL, hashes them, and verifies those hashes exist as tags in the cache. Or we just store etags from source files as tags in the cache, i.e, "input:asf321qfwafs"

Using the existing purge infrastructure, we would need to delete all source-cached stuff first, then all variants - but since requests continue to come in, disk locking could be an issue.  Also, search by tag is slow(ish) for hybridcache. We would need to be able to first target source cached proxied copies for deletion before moving on to generated versions that incorporate it.

Alternatively, we offer multiple invalidation APIs

InvalidateAll() - increments a global counter/seed incorporated into every hash. Also available as a config integer

InvalidateSince(DateTime) - would rely on new cache interface, would only delete newly written stuff.

InvalidateSourceSet(list of source URIs)
InvalidateUrls(list of endpoint URIs)

RevalidateSourceSet() - for each cache, for each source (key basis), check that the remote etags haven't diverged (GET with if-none-match) from the cached etags (fall back to bitwise comparison if the remote server doesn't send an etag). If they HAVE diverged, increment the invalidation counter for those sources (but only once, regardless of cache) (which bumps the lookup hash and key basis). Purge the old key basis (in case our invalidation set gets wiped).

RevalidateSourcesFor() - using execution plans, revalidate all involved sources.

For both above, allow config for the source to go 404. I.e, if the resource is no longer accessible, ensure all dependent requests now fail instead of cache hitting.

Note that we need a plan for throttling failed requests. 502 bad gateway results can be cached for a limited time, in memory only.  upstream 403/404 results should also be memcached, for a longer time.


Perhaps we have additional bloom filters (remember, we only used 24 of the 256 bits available to us) 


TODO: notify the cache when an entry has been invalidated and usage is likely to cease. This, however, disregards the more frequent cases where global settings cause all cache entries to invalidate, or URLs are refactored.







