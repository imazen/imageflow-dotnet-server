using Imageflow.Fluent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Imageflow.Server.Extensibility.ClassicDiskCache;
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
        private readonly BlobProvider blobProvider;
        private readonly DiagnosticsPage diagnosticsPage;
        private readonly ImageflowMiddlewareOptions options;
        public ImageflowMiddleware(RequestDelegate next, IWebHostEnvironment env, ILogger<ImageflowMiddleware> logger, IMemoryCache memoryCache, IDistributedCache distributedCache,  IClassicDiskCache diskCache, IEnumerable<IBlobProvider> blobProviders, ImageflowMiddlewareOptions options)
        {
            this.next = next;
            this.options = options;
            this.env = env;
            this.logger = logger;
            this.memoryCache = memoryCache;
            this.diskCache = diskCache;
            var providers = blobProviders.ToList();
            this.blobProvider = new BlobProvider(providers, this.env.WebRootPath);
            diagnosticsPage = new DiagnosticsPage(env, logger, memoryCache, distributedCache, diskCache, providers);
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path;

            // Delegate to the diagnostics page if it is requested
            if (diagnosticsPage.MatchesPath(path.Value))
            {
                await diagnosticsPage.Invoke(context);
                return;
            }
            
            // We only handle requests with an image extension, period. 
            if (!PathHelpers.IsImagePath(path))
            {
                await next.Invoke(context);
                return;
            }


            var imageJobInfo = new ImageJobInfo(path.Value, context.Request.Query, options.NamedWatermarks, blobProvider);
            
            // If the file is definitely missing hand to the next middleware
            // Remote providers will fail late rather than make 2 requests
            if (!imageJobInfo.PrimaryBlobMayExist())
            {
                await next.Invoke(context);
                return;
            }


            
            var memoryCacheEnabled = memoryCache != null && options.AllowMemoryCaching;
            var diskCacheEnabled = diskCache != null && options.AllowDiskCaching;
            var distributedCacheEnabled = distributedCache != null && options.AllowDistributedCaching;
            

            if (memoryCacheEnabled || diskCacheEnabled || distributedCacheEnabled)
            {

                var cacheKey = await imageJobInfo.GetFastCacheKey();

                if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var etag) && cacheKey == etag)
                {
                    context.Response.StatusCode = StatusCodes.Status304NotModified;
                    context.Response.ContentLength = 0;
                    context.Response.ContentType = null;
                    return;
                }

                if (diskCacheEnabled)
                {
                    await ProcessWithDiskCache(context, cacheKey, imageJobInfo);
                }
                else if (memoryCacheEnabled)
                {
                    await ProcessWithMemoryCache(context, cacheKey, imageJobInfo);
                }else if (distributedCacheEnabled)
                {
                    await ProcessWithDistributedCache(context, cacheKey, imageJobInfo);
                }

            }
            else
            {
                await ProcessWithNoCache(context,  imageJobInfo);
            }
        }

        private async Task ProcessWithDiskCache(HttpContext context, string cacheKey, ImageJobInfo info)
        {
            var cacheResult = await diskCache.GetOrCreate(cacheKey, info.EstimatedFileExtension, async (stream) =>
            {
                if (info.HasParams)
                {
                    logger.LogInformation($"DiskCache Miss: Processing image {info.VirtualPath}?{info}");

 
                    var result = await GetImageData(info);
                    await stream.WriteAsync(result.ResultBytes.Array, result.ResultBytes.Offset,
                        result.ResultBytes.Count,
                        CancellationToken.None);
                }
                else
                {
                    logger.LogInformation($"DiskCache Miss: Proxying image {info.VirtualPath}");
                    
                    await using var sourceStream = (await info.GetPrimaryBlob()).OpenReadAsync();
                    await sourceStream.CopyToAsync(stream);
                }
            });

            // Note that using estimated file extension instead of parsing magic bytes will lead to incorrect content-type
            // values when the source file has a mismatched extension.

            if (cacheResult.Data != null)
            {
                context.Response.ContentType = PathHelpers.ContentTypeFor(info.EstimatedFileExtension);
                context.Response.ContentLength = cacheResult.Data.Length;
                context.Response.Headers[HeaderNames.ETag] = cacheKey;
                await cacheResult.Data.CopyToAsync(context.Response.Body);
            }
            else
            {
                logger.LogInformation("Serving {0}?{1} from disk cache {2}", info.VirtualPath, info.CommandString, cacheResult.RelativePath);
                await ServeFileFromDisk(context, cacheResult.PhysicalPath, cacheKey,
                    PathHelpers.ContentTypeFor(info.EstimatedFileExtension));
            }
        }

        private static async Task ServeFileFromDisk(HttpContext context, string path, string etag, string contentType)
        {
            await using var readStream = File.OpenRead(path);
            context.Response.ContentLength = readStream.Length;
            context.Response.ContentType = contentType;
            context.Response.Headers[HeaderNames.ETag] = etag;
            await readStream.CopyToAsync(context.Response.Body);
        }

        private async Task ProcessWithMemoryCache(HttpContext context, string cacheKey, ImageJobInfo info)
        {
            var isCached = memoryCache.TryGetValue(cacheKey, out ArraySegment<byte> imageBytes);
            var isContentTypeCached = memoryCache.TryGetValue(cacheKey + ".contentType", out string contentType);
            if (isCached && isContentTypeCached)
            {
                logger.LogInformation("Serving {0}?{1} from memory cache", info.VirtualPath, info.CommandString);
            }
            else
            {
                
                if (info.HasParams)
                {
                    logger.LogInformation($"Memory Cache Miss: Processing image {info.VirtualPath}?{info.CommandString}");

                    var imageData = await GetImageData(info);
                    imageBytes = imageData.ResultBytes;
                    contentType = imageData.ContentType;
                }
                else
                {
                    logger.LogInformation($"Memory Cache Miss: Proxying image {info.VirtualPath}?{info.CommandString}");

                    contentType = PathHelpers.ContentTypeFor(info.EstimatedFileExtension);
                    await using var sourceStream = (await info.GetPrimaryBlob()).OpenReadAsync();
                    var ms = new MemoryStream((int)sourceStream.Length);
                    await sourceStream.CopyToAsync(ms);
                    imageBytes = new ArraySegment<byte>(ms.GetBuffer());
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
            context.Response.Headers[HeaderNames.ETag] = cacheKey;
            context.Response.ContentLength = imageBytes.Count;
            

            await context.Response.Body.WriteAsync(imageBytes.Array, imageBytes.Offset, imageBytes.Count);
        }

        private async Task ProcessWithDistributedCache(HttpContext context, string cacheKey, ImageJobInfo info)
        {
            var imageBytes = await distributedCache.GetAsync(cacheKey);
            var contentType = await distributedCache.GetStringAsync(cacheKey + ".contentType");
            if (imageBytes != null && contentType != null)
            {
                logger.LogInformation("Serving {0}?{1} from distributed cache", info.VirtualPath, info.CommandString);
            }
            else
            {

               
                if (info.HasParams)
                {
                    logger.LogInformation($"Distributed Cache Miss: Processing image {info.VirtualPath}?{info.CommandString}");

                    var imageData = await GetImageData(info);
                    imageBytes = imageData.ResultBytes.Count != imageData.ResultBytes.Array?.Length 
                        ? imageData.ResultBytes.ToArray() 
                        : imageData.ResultBytes.Array;

                    contentType = imageData.ContentType;
                }
                else
                {
                    logger.LogInformation($"Distributed Cache Miss: Proxying image {info.VirtualPath}?{info.CommandString}");

                    contentType = PathHelpers.ContentTypeFor(info.EstimatedFileExtension);
                    await using var sourceStream = (await info.GetPrimaryBlob()).OpenReadAsync();
                    var ms = new MemoryStream((int)sourceStream.Length);
                    await sourceStream.CopyToAsync(ms);
                    imageBytes = ms.GetBuffer();
                }

                // Set cache options.
                var cacheEntryOptions = new DistributedCacheEntryOptions()
                    .SetSlidingExpiration(options.DistributedCacheSlidingExpiration);
    
                await distributedCache.SetAsync(cacheKey, imageBytes, cacheEntryOptions);
                await distributedCache.SetStringAsync(cacheKey + ".contentType", contentType, cacheEntryOptions);
            }

            // write to stream
            context.Response.ContentType = contentType;
            context.Response.Headers[HeaderNames.ETag] = cacheKey;
            context.Response.ContentLength = imageBytes.Length;
            
            await context.Response.Body.WriteAsync(imageBytes, 0, imageBytes.Length);
        }
           
        private async Task ProcessWithNoCache(HttpContext context, ImageJobInfo info)
        {

            
            // If we're not caching, we should always use the modified date from source blobs as part of the etag
            
            var betterCacheKey = await info.GetExactCacheKey();
            if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var etag) && betterCacheKey == etag)
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                context.Response.ContentLength = 0;
                context.Response.ContentType = null;
                return;
            }
            
            if (info.HasParams)
            {
                logger.LogInformation($"Processing image {info.VirtualPath} with params {info.CommandString}");

                var imageData = await GetImageData(info);
                var imageBytes = imageData.ResultBytes;
                var contentType = imageData.ContentType;

                // write to stream
                context.Response.ContentType = contentType;
                context.Response.Headers[HeaderNames.ETag] = betterCacheKey;
                context.Response.ContentLength = imageBytes.Count;

                await context.Response.Body.WriteAsync(imageBytes.Array, imageBytes.Offset, imageBytes.Count);
            }
            else
            {
                logger.LogInformation($"Proxying image {info.VirtualPath} with params {info.CommandString}");

                var contentType = PathHelpers.ContentTypeFor(info.EstimatedFileExtension);
                await using var sourceStream = (await info.GetPrimaryBlob()).OpenReadAsync();
                context.Response.ContentType = contentType;
                context.Response.Headers[HeaderNames.ETag] = betterCacheKey;
                context.Response.ContentLength = sourceStream.Length;

                await sourceStream.CopyToAsync(context.Response.Body);
            }
            

        }

     
        private Task<ImageData> GetImageData(ImageJobInfo info)
        {
            return info.ProcessUncached();
        }
        
    }
}
