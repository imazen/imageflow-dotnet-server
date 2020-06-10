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

            var blobResult = blobProvider.GetBlobResult(path);
            // We want to proxy unmodified blob images but not files
            var resizeParams = PathHelpers.GetResizeParams(Path.GetExtension(path.Value), context.Request.Query);
            if (blobResult == null && !resizeParams.HasParams)
            {
                await next.Invoke(context);
                return;
            }

            // Now we see if the file exists
            blobResult ??= blobProvider.GetFileResult(path);

            // If the file is missing hand to the next middleware
            if (blobResult == null)
            {
                await next.Invoke(context);
                return;
            }

            var memoryCacheEnabled = memoryCache != null && options.AllowMemoryCaching;
            var diskCacheEnabled = diskCache != null && options.AllowDiskCaching;
            var distributedCacheEnabled = distributedCache != null && options.AllowDistributedCaching;
            

            if (memoryCacheEnabled || diskCacheEnabled || distributedCacheEnabled)
            {
                var lastModifiedUtcMaybe = blobResult.Value.IsFile
                    ? (await blobResult.Value.GetBlob()).LastModifiedDateUtc ?? DateTime.MinValue
                    : DateTime.MinValue;

                var cacheKey = PathHelpers.GetCacheKey(path, resizeParams, lastModifiedUtcMaybe);


                if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var etag) && cacheKey == etag)
                {
                    context.Response.StatusCode = StatusCodes.Status304NotModified;
                    context.Response.ContentLength = 0;
                    context.Response.ContentType = null;
                    return;
                }

                if (diskCacheEnabled)
                {
                    await ProcessWithDiskCache(context, cacheKey, path.Value, blobResult.Value.GetBlob, resizeParams);
                }
                else if (memoryCacheEnabled)
                {
                    await ProcessWithMemoryCache(context, cacheKey, path.Value, blobResult.Value.GetBlob, resizeParams);
                }

            }
            else
            {
                await ProcessWithNoCache(context, path.Value, blobResult.Value.GetBlob, resizeParams);
            }
        }

        private async Task ProcessWithDiskCache(HttpContext context, string cacheKey, string webPath, Func<Task<IBlobData>> getBlob,
            ResizeParams commands)
        {
            var cacheResult = await diskCache.GetOrCreate(cacheKey, commands.EstimatedFileExtension, async (stream) =>
            {
                using var blob = await getBlob();
                if (commands.HasParams)
                {
                    logger.LogInformation($"DiskCache Miss: Processing image {webPath}?{commands}");

 
                    var result = await GetImageData(blob, commands.CommandString);
                    await stream.WriteAsync(result.ResultBytes.Array, result.ResultBytes.Offset,
                        result.ResultBytes.Count,
                        CancellationToken.None);
                }
                else
                {
                    logger.LogInformation($"DiskCache Miss: Proxying image {webPath}");
                    
                    await using var sourceStream = blob.OpenReadAsync();
                    await sourceStream.CopyToAsync(stream);
                }
            });

            // Note that using estimated file extension instead of parsing magic bytes will lead to incorrect content-type
            // values when the source file has a mismatched extension.

            if (cacheResult.Data != null)
            {
                context.Response.ContentType = PathHelpers.ContentTypeFor(commands.EstimatedFileExtension);
                context.Response.ContentLength = cacheResult.Data.Length;
                context.Response.Headers[HeaderNames.ETag] = cacheKey;
                await cacheResult.Data.CopyToAsync(context.Response.Body);
            }
            else
            {
                logger.LogInformation("Serving {0}?{1} from disk cache {2}", webPath, commands.CommandString, cacheResult.RelativePath);
                await ServeFileFromDisk(context, cacheResult.PhysicalPath, cacheKey,
                    PathHelpers.ContentTypeFor(commands.EstimatedFileExtension));
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

        private async Task ProcessWithMemoryCache(HttpContext context, string cacheKey, string webPath, Func<Task<IBlobData>> getBlob, ResizeParams commands)
        {
            var isCached = memoryCache.TryGetValue(cacheKey, out ArraySegment<byte> imageBytes);
            var isContentTypeCached = memoryCache.TryGetValue(cacheKey + ".contentType", out string contentType);
            if (isCached && isContentTypeCached)
            {
                logger.LogInformation("Serving {0}?{1} from memory cache", webPath, commands.CommandString);
            }
            else
            {

                using var blob = await getBlob();
                if (commands.HasParams)
                {
                    logger.LogInformation($"Memory Cache Miss: Processing image {webPath}?{commands}");

                    var imageData = await GetImageData(blob, commands.CommandString);
                    imageBytes = imageData.ResultBytes;
                    contentType = imageData.ContentType;
                }
                else
                {
                    logger.LogInformation($"Memory Cache Miss: Proxying image {webPath}?{commands}");

                    contentType = PathHelpers.ContentTypeFor(commands.EstimatedFileExtension);
                    await using var sourceStream = blob.OpenReadAsync();
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

        private async Task ProcessWithDistributedCache(HttpContext context, string cacheKey, string webPath, Func<Task<IBlobData>> getBlob, ResizeParams commands)
        {
            var imageBytes = await distributedCache.GetAsync(cacheKey);
            var contentType = await distributedCache.GetStringAsync(cacheKey + ".contentType");
            if (imageBytes != null && contentType != null)
            {
                logger.LogInformation("Serving {0}?{1} from distributed cache", webPath, commands.CommandString);
            }
            else
            {

                using var blob = await getBlob();
                if (commands.HasParams)
                {
                    logger.LogInformation($"Distributed Cache Miss: Processing image {webPath}?{commands}");

                    var imageData = await GetImageData(blob, commands.CommandString);
                    imageBytes = imageData.ResultBytes.Count != imageData.ResultBytes.Array?.Length 
                        ? imageData.ResultBytes.ToArray() 
                        : imageData.ResultBytes.Array;

                    contentType = imageData.ContentType;
                }
                else
                {
                    logger.LogInformation($"Distributed Cache Miss: Proxying image {webPath}?{commands}");

                    contentType = PathHelpers.ContentTypeFor(commands.EstimatedFileExtension);
                    await using var sourceStream = blob.OpenReadAsync();
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
           
        private async Task ProcessWithNoCache(HttpContext context, string webPath, Func<Task<IBlobData>> getBlob,
            ResizeParams commands)
        {

            
            // If we're not caching, we should always use the modified date from source blobs as part of the etag
            using var blob = await getBlob();
            var lastModifiedUtcMaybe = blob.LastModifiedDateUtc ?? DateTime.MinValue;
            var betterCacheKey = PathHelpers.GetCacheKey(webPath, commands, lastModifiedUtcMaybe);
            if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var etag) && betterCacheKey == etag)
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                context.Response.ContentLength = 0;
                context.Response.ContentType = null;
                return;
            }
            
            if (commands.HasParams)
            {
                logger.LogInformation($"Processing image {webPath} with params {commands}");

                var imageData = await GetImageData(blob, commands.CommandString);
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
                logger.LogInformation($"Proxying image {webPath} with params {commands}");

                var contentType = PathHelpers.ContentTypeFor(commands.EstimatedFileExtension);
                await using var sourceStream = blob.OpenReadAsync();
                context.Response.ContentType = contentType;
                context.Response.Headers[HeaderNames.ETag] = betterCacheKey;
                context.Response.ContentLength = sourceStream.Length;

                await sourceStream.CopyToAsync(context.Response.Body);
            }
            

        }

     
        private async Task<ImageData> GetImageData(IBlobData blob, string querystringCommands)
        {
            using var buildJob = new FluentBuildJob();
            var jobResult = await buildJob.BuildCommandString(
                    new StreamSource(blob.OpenReadAsync(), true),
                    new BytesDestination(), querystringCommands)
                .Finish()
                .InProcessAsync();

            var bytes = jobResult.First.TryGetBytes().Value;

            return new ImageData
            {
                ContentType = jobResult.First.PreferredMimeType,
                FileExtension = jobResult.First.PreferredExtension,
                ResultBytes = bytes
            };
        }
        
    }
}
