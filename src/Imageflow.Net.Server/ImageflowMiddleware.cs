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
using System.Threading.Tasks;

namespace Imageflow.Server
{
    public class ImageflowMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ImageflowMiddleware> _logger;
        private readonly IHostingEnvironment _env;
        private readonly IMemoryCache _memoryCache;

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

        public ImageflowMiddleware(RequestDelegate next, IHostingEnvironment env, ILogger<ImageflowMiddleware> logger, IMemoryCache memoryCache)
        {
            _next = next;
            _env = env;
            _logger = logger;
            _memoryCache = memoryCache;
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
            var resizeParams = GetResizeParams(context.Request.Query);
            if (!resizeParams.hasParams)
            {
                await _next.Invoke(context);
                return;
            }

            // get the image location on disk
            var imagePath = Path.Combine(
                _env.WebRootPath,
                path.Value.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));

            // check file lastwrite
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(imagePath);
            if (lastWriteTimeUtc.Year == 1601) // file doesn't exist, pass to next middleware
            {
                await _next.Invoke(context);
                return;
            }

            // if we got this far, resize it
            _logger.LogInformation($"Processing image {path.Value} with params {resizeParams}");

            string cacheKey = GetCacheKey(imagePath, resizeParams, lastWriteTimeUtc);

            bool isCached = _memoryCache.TryGetValue(cacheKey, out ArraySegment<byte> imageBytes);
            bool isContentTypeCached = _memoryCache.TryGetValue(cacheKey + ".contentType", out string contentType);
            if (isCached && isContentTypeCached)
            {
                _logger.LogInformation("Serving from cache");
            }
            else
            {
                var imageData = await GetImageData(imagePath, resizeParams);
                imageBytes = imageData.resultBytes;
                contentType = imageData.contentType;

                _memoryCache.Set(cacheKey, imageBytes);
                _memoryCache.Set(cacheKey + ".contentType", contentType);
            }

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

        private ResizeParams GetResizeParams(IQueryCollection query)
        {
            var resizeParams = new ResizeParams
            {
                hasParams = querystringKeys.Any(f => query.ContainsKey(f))
            };

            // if no params present, quit early
            if (!resizeParams.hasParams)
                return resizeParams;

            // extract resize params
            resizeParams.commandString = string.Join("&", MatchingResizeQueryStringParameters(query));

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
            // check cache and return if cached
            return string.Format("{0}?{1}|{2}", imagePath, resizeParams.ToString(), lastWriteTimeUtc);
        }

        private async Task<ImageData> GetImageData(string imagePath, ResizeParams resizeParams)
        {
            using (var buildJob = new FluentBuildJob())
            {
                var jobResult = await buildJob.Decode(new StreamSource(File.OpenRead(imagePath), true))
                    .ResizerCommands(resizeParams.commandString)
                    .EncodeToBytes(new WebPLossyEncoder(DefaultWebPLossyEncoderQuality))
                    .Finish()
                    .InProcessAsync();

                var bytes = jobResult.First.TryGetBytes().Value;

                return new ImageData { contentType = jobResult.First.PreferredMimeType, resultBytes = bytes };
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
