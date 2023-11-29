
#### Breaking changes in v0.9 rewrite

This version is a complete rewrite of the product to enable tons of key scenarios.

# Rewrite, PreRewriteAuth and PostRewriteAuth have changed.
UrlEventArgs.Query was Dictionary<string,string> and is now IDictionary<string,StringValues>
UrlEventArgs.Context is no longer available. Use UrlEventArgs.Request.OriginalRequest to access 
some of the same properties. 

Alternate idea: .Context is (object) instead. It could be a HttpContext, or not. 

I'm currently bogged down in a massive Imageflow Server refactor that will
enable a lot of new scenarios (and be usable from .NET 4.8 as well as .NET
Standard 2, .NET 6-8, but it also heavily architected. It creates new nuget
packages Imazen.Routing and Imazen.Abstractions, with the intent to support
serverless/one-shot/AOT mode as well as long-running server processes that
can apply a lot more heuristics and optimizations. It introduces a ton of
new interfaces and establishes a mini-framework for blobs with optimized
routing, resource dependency discovery for smart cache keys, etags, smart
buffering, async cache writes, memory caching, blob caching, Pipelines
support, cache invalidation, search, audit trails, and purging, better
non-image blob handling, intuive route->provider mapping, and can support
future real-time resilience features like circuit breaker, multi-cache
choice, and possibly offloading jobs to other servers in the cluster or
serverless functions. It will autoconfigure s3 and azure containers for
maximum throughout and automatic eviction of expired unused entries.

This also lays the foundation for AI salience detection and storage of
metadata like this anchor value, so that it could automatically happen if a
salience backend/service and cache service are registered.

It's a lot, and I could use some code review when I wrap up the first
preview. I think it would be usable directly in your packages that are
alternatives to ImageResizer. I could probably do this side quest for
anchor support once I have a preview shipped and reviewed.
