using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Imageflow.Server.Extensibility;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.Common.Instrumentation;
using Imazen.Common.Instrumentation.Support;
using Imazen.Common.Licensing;
using Imazen.Common.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Net.Http.Headers;

namespace Imageflow.Server
{
    public class ImageflowMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger<ImageflowMiddleware> logger;
        private readonly IWebHostEnvironment env;
        private readonly IMemoryCache memoryCache;
        private readonly IDistributedCache distributedCache;
        private readonly IClassicDiskCache diskCache;
        private readonly ISqliteCache sqliteCache;
        private readonly BlobProvider blobProvider;
        private readonly DiagnosticsPage diagnosticsPage;
        private readonly LicensePage licensePage;
        private readonly ImageflowMiddlewareOptions options;
        private readonly GlobalInfoProvider globalInfoProvider;
        public ImageflowMiddleware(
            RequestDelegate next, 
            IWebHostEnvironment env, 
            IEnumerable<ILogger<ImageflowMiddleware>> logger, 
            IEnumerable<IMemoryCache> memoryCache, 
            IEnumerable<IDistributedCache> distributedCache, 
            IEnumerable<ISqliteCache> sqliteCaches,
            IEnumerable<IClassicDiskCache> diskCache, 
            IEnumerable<IBlobProvider> blobProviders, 
            ImageflowMiddlewareOptions options)
        {
            this.next = next;
            options.Licensing ??= new Licensing(LicenseManagerSingleton.GetOrCreateSingleton(
                "imageflow_", new[] {env.ContentRootPath, Path.GetTempPath()}));
            this.options = options;
            this.env = env;
            this.logger = logger.FirstOrDefault();
            this.memoryCache = memoryCache.FirstOrDefault();
            this.diskCache = diskCache.FirstOrDefault();
            this.distributedCache = distributedCache.FirstOrDefault();
            this.sqliteCache = sqliteCaches.FirstOrDefault();
            var providers = blobProviders.ToList();
            var mappedPaths = options.MappedPaths.ToList();
            if (options.MapWebRoot)
            {
                if (this.env?.WebRootPath == null)
                    throw new InvalidOperationException("Cannot call MapWebRoot if env.WebRootPath is null");
                mappedPaths.Add(new PathMapping("/", this.env.WebRootPath));
            }
            
            //Determine the active cache backend
            var memoryCacheEnabled = this.memoryCache != null && options.AllowMemoryCaching;
            var diskCacheEnabled = this.diskCache != null && options.AllowDiskCaching;
            var distributedCacheEnabled = this.distributedCache != null && options.AllowDistributedCaching;
            var sqliteCacheEnabled = this.sqliteCache != null && options.AllowSqliteCaching;
            
            if (sqliteCacheEnabled)
                options.ActiveCacheBackend = CacheBackend.SqliteCache;
            else if (diskCacheEnabled)
                options.ActiveCacheBackend = CacheBackend.ClassicDiskCache;
            else if (memoryCacheEnabled)
                options.ActiveCacheBackend = CacheBackend.MemoryCache;
            else if (distributedCacheEnabled)
                options.ActiveCacheBackend = CacheBackend.DistributedCache;
            else
                options.ActiveCacheBackend = CacheBackend.NoCache;
            
            
            
            options.Licensing.Initialize(this.options);

            blobProvider = new BlobProvider(providers, mappedPaths);
            diagnosticsPage = new DiagnosticsPage(options, env, this.logger, this.sqliteCache, this.memoryCache, this.distributedCache, this.diskCache, providers);
            licensePage = new LicensePage(options);
            globalInfoProvider = new GlobalInfoProvider(options, env, this.logger, this.sqliteCache, this.memoryCache, this.distributedCache, this.diskCache, providers);
            
            GlobalPerf.Singleton.SetInfoProviders(new List<IInfoProvider>(){globalInfoProvider});
        }

        public async Task Invoke(HttpContext context)
        {
            // For instrumentation
            globalInfoProvider.CopyHttpContextInfo(context);
            
            var path = context.Request.Path;

            // Delegate to the diagnostics page if it is requested
            if (diagnosticsPage.MatchesPath(path.Value))
            {
                await diagnosticsPage.Invoke(context);
                return;
            }
            // Delegate to licenses page if requested
            if (licensePage.MatchesPath(path.Value))
            {
                await licensePage.Invoke(context);
                return;
            }

            // We only handle requests with an image extension or if we configured a path prefix for which to handle
            // extensionless requests
            
            if (!ImageJobInfo.ShouldHandleRequest(context, options, blobProvider))
            {
                await next.Invoke(context);
                return;
            }
            
            var imageJobInfo = new ImageJobInfo(context, options, blobProvider);

            if (!imageJobInfo.Authorized)
            {
                await NotAuthorized(context, imageJobInfo.AuthorizedMessage);
                return;
            }

            if (imageJobInfo.LicenseError)
            {
                if (options.EnforcementMethod == EnforceLicenseWith.Http422Error)
                {
                    await StringResponseNoCache(context, 422, options.Licensing.InvalidLicenseMessage);
                    return;
                }
                if (options.EnforcementMethod == EnforceLicenseWith.Http402Error)
                {
                    await StringResponseNoCache(context, 402, options.Licensing.InvalidLicenseMessage);
                    return;
                }
            }

            // If the file is definitely missing hand to the next middleware
            // Remote providers will fail late rather than make 2 requests
            if (!imageJobInfo.PrimaryBlobMayExist())
            {
                await next.Invoke(context);
                return;
            }
            
            string cacheKey = null;
            var cachingPath = imageJobInfo.NeedsCaching() ? options.ActiveCacheBackend : CacheBackend.NoCache;
            if (cachingPath != CacheBackend.NoCache)
            {
                cacheKey = await imageJobInfo.GetFastCacheKey();

                if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var etag) && cacheKey == etag)
                {
                    GlobalPerf.Singleton.IncrementCounter("etag_hit");
                    context.Response.StatusCode = StatusCodes.Status304NotModified;
                    context.Response.ContentLength = 0;
                    context.Response.ContentType = null;
                    return;
                }
                GlobalPerf.Singleton.IncrementCounter("etag_miss");
            }

            try
            {
                switch (cachingPath)
                {
                    case CacheBackend.ClassicDiskCache:
                        await ProcessWithDiskCache(context, cacheKey, imageJobInfo);
                        break;
                    case CacheBackend.SqliteCache:
                        await ProcessWithSqliteCache(context, cacheKey, imageJobInfo);
                        break;
                    case CacheBackend.MemoryCache:
                        await ProcessWithMemoryCache(context, cacheKey, imageJobInfo);
                        break;
                    case CacheBackend.DistributedCache:
                        await ProcessWithDistributedCache(context, cacheKey, imageJobInfo);
                        break;
                    case CacheBackend.NoCache:
                        await ProcessWithNoCache(context, imageJobInfo);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                GlobalPerf.Singleton.IncrementCounter("middleware_ok");
            }
            catch (BlobMissingException e)
            {
                await NotFound(context, e);
            }
            catch (Exception e)
            {
                var errorName = e.GetType()?.Name ?? "unknown";
                var errorCounter = "middleware_" + errorName;
                GlobalPerf.Singleton.IncrementCounter(errorCounter);
                GlobalPerf.Singleton.IncrementCounter("middleware_errors");
                throw;
            }
            finally
            {
                // Increment counter for type of file served
                var imageExtension = PathHelpers.GetImageExtensionFromContentType(context.Response.ContentType);
                if (imageExtension != null)
                {
                    GlobalPerf.Singleton.IncrementCounter("module_response_ext_" + imageExtension);
                }
            }
        }

        private async Task NotAuthorized(HttpContext context, string detail)
        {
            var s = "You are not authorized to access the given resource.";
            if (!string.IsNullOrEmpty(detail))
            {
                s += "\r\n" + detail;
            }
            GlobalPerf.Singleton.IncrementCounter("http_403");
            await StringResponseNoCache(context, 403, s);
        }
        
        private async Task NotFound(HttpContext context, BlobMissingException e)
        {
            GlobalPerf.Singleton.IncrementCounter("http_404");
            // We allow 404s to be cached, but not 403s or license errors
            var s = "The specified resource does not exist.\r\n" + e.Message;
            context.Response.StatusCode = 404;
            context.Response.ContentType = "text/plain; charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(s);
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
        private async Task StringResponseNoCache(HttpContext context, int statusCode, string contents)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.Headers[HeaderNames.CacheControl] = "no-store";
            var bytes = Encoding.UTF8.GetBytes(contents);
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        private async Task ProcessWithDiskCache(HttpContext context, string cacheKey, ImageJobInfo info)
        {
            var cacheResult = await diskCache.GetOrCreate(cacheKey, info.EstimatedFileExtension, async (stream) =>
            {
                if (info.HasParams)
                {
                    logger?.LogInformation($"DiskCache Miss: Processing image {info.FinalVirtualPath}?{info}");

 
                    var result = await info.ProcessUncached();
                    await stream.WriteAsync(result.ResultBytes.Array, result.ResultBytes.Offset,
                        result.ResultBytes.Count,
                        CancellationToken.None);
                    await stream.FlushAsync();
                }
                else
                {
                    logger?.LogInformation($"DiskCache Miss: Proxying image {info.FinalVirtualPath}");
                    await info.CopyPrimaryBlobToAsync(stream);
                }
            });
            
            if (cacheResult.Result == CacheQueryResult.Miss)
            {
                GlobalPerf.Singleton.IncrementCounter("diskcache_miss");
            }
            else if (cacheResult.Result == CacheQueryResult.Hit)
            {
                GlobalPerf.Singleton.IncrementCounter("diskcache_hit");
            }
            else if (cacheResult.Result == CacheQueryResult.Failed)
            {
                GlobalPerf.Singleton.IncrementCounter("diskcache_timeout");
            }

            // Note that using estimated file extension instead of parsing magic bytes will lead to incorrect content-type
            // values when the source file has a mismatched extension.

            if (cacheResult.Data != null)
            {
                if (cacheResult.Data.Length < 1)
                {
                    throw new InvalidOperationException("DiskCache returned cache entry with zero bytes");
                }
                SetCachingHeaders(context, cacheKey);
                await MagicBytes.ProxyToStream(cacheResult.Data, context.Response);
            }
            else
            {
                logger?.LogInformation("Serving {0}?{1} from disk cache {2}", info.FinalVirtualPath, info.CommandString, cacheResult.RelativePath);
                await ServeFileFromDisk(context, cacheResult.PhysicalPath, cacheKey);
            }
        }

        private async Task ServeFileFromDisk(HttpContext context, string path, string etag)
        {
            await using var readStream = File.OpenRead(path);
            if (readStream.Length < 1)
            {
                throw new InvalidOperationException("DiskCache file entry has zero bytes");
            }
            SetCachingHeaders(context, etag);
            await MagicBytes.ProxyToStream(readStream, context.Response);
        }

        private async Task ProcessWithMemoryCache(HttpContext context, string cacheKey, ImageJobInfo info)
        {
            var isCached = memoryCache.TryGetValue(cacheKey, out ArraySegment<byte> imageBytes);
            var isContentTypeCached = memoryCache.TryGetValue(cacheKey + ".contentType", out string contentType);
            if (isCached && isContentTypeCached)
            {
                logger?.LogInformation("Serving {0}?{1} from memory cache", info.FinalVirtualPath, info.CommandString);
            }
            else
            {
                
                if (info.HasParams)
                {
                    logger?.LogInformation($"Memory Cache Miss: Processing image {info.FinalVirtualPath}?{info.CommandString}");

                    var imageData = await info.ProcessUncached();
                    imageBytes = imageData.ResultBytes;
                    contentType = imageData.ContentType;
                }
                else
                {
                    logger?.LogInformation($"Memory Cache Miss: Proxying image {info.FinalVirtualPath}?{info.CommandString}");
                    
                    var imageBytesArray = await info.GetPrimaryBlobBytesAsync();
                    contentType = MagicBytes.GetContentTypeFromBytes(imageBytesArray);
                    imageBytes = new ArraySegment<byte>(imageBytesArray);
                }

                // Set cache options.
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSize(imageBytes.Count)
                    .SetSlidingExpiration(options.MemoryCacheSlidingExpiration);
                
                var cacheEntryMetaOptions = new MemoryCacheEntryOptions()
                    .SetSize(contentType.Length * 2)
                    .SetSlidingExpiration(options.MemoryCacheSlidingExpiration);
                
                memoryCache.Set(cacheKey, imageBytes, cacheEntryOptions);
                memoryCache.Set(cacheKey + ".contentType", contentType, cacheEntryMetaOptions);
            }

            // write to stream
            context.Response.ContentType = contentType;
            context.Response.ContentLength = imageBytes.Count;
            SetCachingHeaders(context, cacheKey);


            await context.Response.Body.WriteAsync(imageBytes.Array, imageBytes.Offset, imageBytes.Count);
        }

        private async Task ProcessWithDistributedCache(HttpContext context, string cacheKey, ImageJobInfo info)
        {
            var imageBytes = await distributedCache.GetAsync(cacheKey);
            var contentType = await distributedCache.GetStringAsync(cacheKey + ".contentType");
            if (imageBytes != null && contentType != null)
            {
                logger?.LogInformation("Serving {0}?{1} from distributed cache", info.FinalVirtualPath, info.CommandString);
            }
            else
            {

               
                if (info.HasParams)
                {
                    logger?.LogInformation($"Distributed Cache Miss: Processing image {info.FinalVirtualPath}?{info.CommandString}");

                    var imageData = await info.ProcessUncached();
                    imageBytes = imageData.ResultBytes.Count != imageData.ResultBytes.Array?.Length 
                        ? imageData.ResultBytes.ToArray() 
                        : imageData.ResultBytes.Array;

                    contentType = imageData.ContentType;
                }
                else
                {
                    logger?.LogInformation($"Distributed Cache Miss: Proxying image {info.FinalVirtualPath}?{info.CommandString}");
                    
                    imageBytes = await info.GetPrimaryBlobBytesAsync();
                    contentType = MagicBytes.GetContentTypeFromBytes(imageBytes);
                }

                // Set cache options.
                var cacheEntryOptions = new DistributedCacheEntryOptions()
                    .SetSlidingExpiration(options.DistributedCacheSlidingExpiration);
    
                await distributedCache.SetAsync(cacheKey, imageBytes, cacheEntryOptions);
                await distributedCache.SetStringAsync(cacheKey + ".contentType", contentType, cacheEntryOptions);
            }

            // write to stream
            context.Response.ContentType = contentType;
            context.Response.ContentLength = imageBytes.Length;
            SetCachingHeaders(context, cacheKey);

            await context.Response.Body.WriteAsync(imageBytes, 0, imageBytes.Length);
        }
        private async Task ProcessWithSqliteCache(HttpContext context, string cacheKey, ImageJobInfo info)
        {
            var cacheResult = await sqliteCache.GetOrCreate(cacheKey, async () =>
            {
                if (info.HasParams)
                {
                    logger?.LogInformation($"Sqlite Cache Miss: Processing image {info.FinalVirtualPath}?{info.CommandString}");

                    var imageData = await info.ProcessUncached();
                    var imageBytes = imageData.ResultBytes.Count != imageData.ResultBytes.Array?.Length
                        ? imageData.ResultBytes.ToArray()
                        : imageData.ResultBytes.Array;

                    var contentType = imageData.ContentType;
                    return new SqliteCacheEntry()
                    {
                        ContentType = contentType,
                        Data = imageBytes
                    };
                }
                else
                {
                    logger?.LogInformation($"Sqlite Cache Miss: Proxying image {info.FinalVirtualPath}?{info.CommandString}");

                    var data = await info.GetPrimaryBlobBytesAsync();
                    return new SqliteCacheEntry()
                    {
                        ContentType = MagicBytes.GetContentTypeFromBytes(data),
                        Data = data
                    };
                }
            });
            

            // write to stream
            context.Response.ContentType = cacheResult.ContentType;
            context.Response.ContentLength = cacheResult.Data.Length;
            SetCachingHeaders(context, cacheKey);

            await context.Response.Body.WriteAsync(cacheResult.Data, 0, cacheResult.Data.Length);
        }
        
        private async Task ProcessWithNoCache(HttpContext context, ImageJobInfo info)
        {

            
            // If we're not caching, we should always use the modified date from source blobs as part of the etag
            
            var betterCacheKey = await info.GetExactCacheKey();
            if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var etag) && betterCacheKey == etag)
            {
                GlobalPerf.Singleton.IncrementCounter("etag_hit");
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                context.Response.ContentLength = 0;
                context.Response.ContentType = null;
                return;
            }
            GlobalPerf.Singleton.IncrementCounter("etag_miss");
            if (info.HasParams)
            {
                logger?.LogInformation($"Processing image {info.FinalVirtualPath} with params {info.CommandString}");
                GlobalPerf.Singleton.IncrementCounter("nocache_processed");
                var imageData = await info.ProcessUncached();
                var imageBytes = imageData.ResultBytes;
                var contentType = imageData.ContentType;

                // write to stream
                context.Response.ContentType = contentType;
                context.Response.ContentLength = imageBytes.Count;
                SetCachingHeaders(context, betterCacheKey);

                await context.Response.Body.WriteAsync(imageBytes.Array, imageBytes.Offset, imageBytes.Count);
            }
            else
            {
                logger?.LogInformation($"Proxying image {info.FinalVirtualPath} with params {info.CommandString}");
                GlobalPerf.Singleton.IncrementCounter("nocache_proxied");
                await using var sourceStream = (await info.GetPrimaryBlob()).OpenRead();
                SetCachingHeaders(context, betterCacheKey);
                await MagicBytes.ProxyToStream(sourceStream, context.Response);
            }
            

        }

        private void SetCachingHeaders(HttpContext context, string etag)
        {
            context.Response.Headers[HeaderNames.ETag] = etag;
            if (options.DefaultCacheControlString != null)
                context.Response.Headers[HeaderNames.CacheControl] = options.DefaultCacheControlString;
        }
        
    }
}
