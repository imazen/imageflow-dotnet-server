using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Imageflow.Server
{
    internal static class PathHelpers
    {
        
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
        
        internal static bool IsImagePath(PathString path)
        {
            if (path == null || !path.HasValue)
                return false;

            return suffixes.Any(suffix => path.Value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        internal static string SanitizeExtension(string extension)
        {
            extension = extension.ToLowerInvariant().TrimStart('.');
            return extension switch
            {
                "png" => "png",
                "gif" => "gif",
                "webp" => "webp",
                "jpeg" => "jpg",
                _ => "jpg"
            };
        }

        internal static string ContentTypeFor(string extension)
        {
            return extension switch
            {
                "png" => "image/png",
                "gif" => "image/gif",
                "jpg" => "image/jpeg",
                "webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }
        internal static ResizeParams GetResizeParams(string sourceFileExtension, IQueryCollection query)
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

        internal static IEnumerable<string> MatchingResizeQueryStringParameters(IQueryCollection queryCollection)
        {
            return querystringKeys
                .Where(queryCollection.ContainsKey)
                .Select(qsKey => qsKey + "=" + queryCollection[qsKey]);
        }

        internal static string GetCacheKey(string imagePath, ResizeParams resizeParams, DateTime lastWriteTimeUtc)
        {
            using var sha2 = SHA256.Create();
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
}