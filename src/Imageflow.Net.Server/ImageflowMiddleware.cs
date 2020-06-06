using Imageflow.Fluent;
using Imageflow.Server.Structs;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Imageflow.Server
{
    public class ImageflowMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ImageflowMiddleware> _logger;
        private readonly IHostingEnvironment _env;
        private readonly IMemoryCache _memoryCache;
        private readonly IClassicDiskCache diskCache;
        private const int DefaultWebPLossyEncoderQuality = 90;

        private static readonly string[] suffixes = new string[] {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".webp"
        };


        private static readonly string[] querystringKeys = new string[]
        {
            "mode", "anchor", "flip", "sflip", "scale", "cache", "process",
            "quality", "zoom", "crop", "cropxunits", "cropyunits",
            "w", "h", "width", "height", "maxwidth", "maxheight", "format", "thumbnail",
             "autorotate", "srotate", "rotate", "ignoreicc",
            "stretch", "webp.lossless", "webp.quality",
            "frame", "page", "subsampling", "colors", "f.sharpen", "f.sharpen_when", "down.colorspace",
            "404", "bgcolor", "paddingcolor", "bordercolor", "preset", "floatspace", "jpeg_idct_downscale_linear", "watermark",
            "s.invert", "s.sepia", "s.grayscale", "s.alpha", "s.brightness", "s.contrast", "s.saturation", "trim.threshold",
            "trim.percentpadding", "a.blur", "a.sharpen", "a.removenoise", "a.balancewhite", "dither", "jpeg.progressive",
            "encoder", "decoder", "builder", "s.roundcorners.", "paddingwidth", "paddingheight", "margin", "borderwidth", "decoder.min_precise_scaling_ratio"
        };

        public ImageflowMiddleware(RequestDelegate next, IHostingEnvironment env, ILogger<ImageflowMiddleware> logger, IMemoryCache memoryCache, IClassicDiskCache diskCache)
        {
            _next = next;
            _env = env;
            _logger = logger;
            _memoryCache = memoryCache;
            this.diskCache = diskCache;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path;

            // hand to next middleware if we are not dealing with an image
            if (context.Request.Query.Count == 0 || !IsImagePath(path))
            {
                await _next.Invoke(context);
                return;
            }

            // hand to next middleware if we are dealing with an image but it doesn't have any usable resize querystring params
            var resizeParams = GetResizeParams(Path.GetExtension(path.Value), context.Request.Query);
            if (!resizeParams.HasParams)
            {
                await _next.Invoke(context);
                return;
            }

            // get the image location on disk
            var imagePath = Path.Combine(
                _env.WebRootPath,
                path.Value.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));

            // check file last write
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(imagePath);
            if (lastWriteTimeUtc.Year == 1601) // file doesn't exist, pass to next middleware
            {
                await _next.Invoke(context);
                return;
            }

            // if we got this far, resize it
            _logger.LogInformation($"Processing image {path.Value} with params {resizeParams}");

            string cacheKey = GetCacheKey(imagePath, resizeParams, lastWriteTimeUtc);
            
            if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var etag) && cacheKey == etag) {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                context.Response.ContentLength = 0;
                context.Response.ContentType = null;
                return;
            }
            

            if (diskCache != null)
            {
                await ProcessWithDiskCache(context, cacheKey, imagePath, resizeParams);
            }
            else if (_memoryCache != null)
            {
                await ProcessWithMemoryCache(context, cacheKey, imagePath, resizeParams);
            }
            else
            {
                await ProcessWithNoCache(context, cacheKey, imagePath, resizeParams);
            }


        }

        private async Task ProcessWithDiskCache(HttpContext context, string cacheKey, string sourceFilePath,
            ResizeParams commands)
        {
            var cacheResult = await diskCache.GetOrCreate(cacheKey, commands.EstimatedFileExtension, async (stream) =>
            {
                var result = await GetImageData(sourceFilePath, commands.CommandString);
                await stream.WriteAsync(result.resultBytes.Array, result.resultBytes.Offset, result.resultBytes.Count,
                    CancellationToken.None);
            });

            // Note that using estimated file extension instead of parsing magic bytes will lead to incorrect content-type
            // values when the source file has a mismatched extension.

            if (cacheResult.Data != null)
            {
                context.Response.ContentType = ContentTypeFor(commands.EstimatedFileExtension);
                context.Response.ContentLength = cacheResult.Data.Length;
                context.Response.Headers[HeaderNames.ETag] = cacheKey;
                await cacheResult.Data.CopyToAsync(context.Response.Body);
            }
            else
            {
                await ServeFileFromDisk(context, cacheResult.PhysicalPath, cacheKey,
                    ContentTypeFor(commands.EstimatedFileExtension));
            }
        }

        private async Task ServeFileFromDisk(HttpContext context, string path, string etag, string contentType)
        {
            using (var readStream = File.OpenRead(path))
            {
                context.Response.ContentLength = readStream.Length;
                context.Response.ContentType = contentType;
                context.Response.Headers[HeaderNames.ETag] = etag;
                await readStream.CopyToAsync(context.Response.Body);
            }
        }

        private async Task ProcessWithMemoryCache(HttpContext context, string cacheKey, string sourceFilePath, ResizeParams commands)
        {
            var isCached = _memoryCache.TryGetValue(cacheKey, out ArraySegment<byte> imageBytes);
            var isContentTypeCached = _memoryCache.TryGetValue(cacheKey + ".contentType", out string contentType);
            if (isCached && isContentTypeCached)
            {
                _logger.LogInformation("Serving from memory cache");
            }
            else
            {
                var imageData = await GetImageData(sourceFilePath, commands.CommandString);
                imageBytes = imageData.resultBytes;
                contentType = imageData.contentType;

                _memoryCache.Set(cacheKey, imageBytes);
                _memoryCache.Set(cacheKey + ".contentType", contentType);
            }

            // write to stream
            context.Response.ContentType = contentType;
            context.Response.Headers[HeaderNames.ETag] = cacheKey;
            context.Response.ContentLength = imageBytes.Count;
            

            await context.Response.Body.WriteAsync(imageBytes.Array, imageBytes.Offset, imageBytes.Count);
        }

        private async Task ProcessWithNoCache(HttpContext context, string cacheKey, string sourceFilePath,
            ResizeParams commands)
        {

            var imageData = await GetImageData(sourceFilePath, commands.CommandString);
            var imageBytes = imageData.resultBytes;
            var contentType = imageData.contentType;

            // write to stream
            context.Response.ContentType = contentType;
            context.Response.ContentLength = imageBytes.Count;

            await context.Response.Body.WriteAsync(imageBytes.Array, imageBytes.Offset, imageBytes.Count);
        }

        private bool IsImagePath(PathString path)
        {
            if (path == null || !path.HasValue)
                return false;

            return suffixes.Any(suffix => path.Value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        private string SanitizeExtension(string extension)
        {
            extension = extension.ToLowerInvariant().TrimStart('.');
            switch (extension)
            {
                case "png":
                    return "png";
                case "gif":
                    return "gif";
                case "webp":
                    return "webp";
                default:
                    return "jpg"; // For jpg, jpe, jif, jfif, jfi, exif, jpeg extensions
            }
        }

        private string ContentTypeFor(string extension)
        {
            switch (extension)
            {
                case "png": return "image/png";
                case "gif": return "image/gif";
                case "jpg": return "image/jpeg";
                case "webp": return "image/webp";
                default:
                    return "application/octet-stream";
            }
        }
        private ResizeParams GetResizeParams(string sourceFileExtension, IQueryCollection query)
        {
            var resizeParams = new ResizeParams
            {
                HasParams = querystringKeys.Any(query.ContainsKey)
            };

            var extension = sourceFileExtension;
            if (query.TryGetValue("format", out var newExtension))
            {
                extension = newExtension;
            }

            resizeParams.EstimatedFileExtension = SanitizeExtension(extension);
                

            // if no params present, quit early
            if (!resizeParams.HasParams)
                return resizeParams;

            // extract resize params
            resizeParams.CommandString = string.Join("&", MatchingResizeQueryStringParameters(query));

            return resizeParams;
        }

        private IEnumerable<string> MatchingResizeQueryStringParameters(IQueryCollection queryCollection)
        {
            return querystringKeys
                .Where(qsKey => queryCollection.ContainsKey(qsKey))
                .Select(qsKey => qsKey + "=" + queryCollection[qsKey]);
        }

        private string GetCacheKey(string imagePath, ResizeParams resizeParams, DateTime lastWriteTimeUtc)
        {
            using (var sha2 = SHA256.Create())
            {
                var stringBytes = Encoding.UTF8.GetBytes($"{imagePath}?{resizeParams.ToString()}|{lastWriteTimeUtc}");
                // check cache and return if cached
                var hashBytes =
                    sha2.ComputeHash(stringBytes);
                return  Convert.ToBase64String(hashBytes)
                    .Replace("=", string.Empty)
                    .Replace('+', '-')
                    .Replace('/', '_');
            }
        }

     
        private async Task<ImageData> GetImageData(string imagePath, string querystringCommands)
        {
            using (var buildJob = new FluentBuildJob())
            {
                var jobResult = await buildJob.Decode(new StreamSource(File.OpenRead(imagePath), true))
                    .ResizerCommands(querystringCommands)
                    .EncodeToBytes(new WebPLossyEncoder(DefaultWebPLossyEncoderQuality))
                    .Finish()
                    .InProcessAsync();

                var bytes = jobResult.First.TryGetBytes().Value;

                return new ImageData { contentType = jobResult.First.PreferredMimeType, fileExtension = jobResult.First.PreferredExtension, resultBytes = bytes };
            }
        }

        // For when we generate etags
        //static string Sha256hex(string input)
        //{
        //    var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        //    return BitConverter.ToString(hash, 0, 4).Replace("-", "").ToLowerInvariant();
        //}

        //static string Sha256Base64(string input)
        //{
        //    var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        //    return ToBase64U(hash);
        //}

        //static string Sha256TruncatedBase64(string input, int bytes)
        //{
        //    var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        //    return ToBase64U(hash.Take(bytes).ToArray());
        //}

        //static string ToBase64U(byte[] data)
        //{
        //    return Convert.ToBase64String(data).Replace("=", String.Empty).Replace('+', '-').Replace('/', '_');
        //}

    }
}
