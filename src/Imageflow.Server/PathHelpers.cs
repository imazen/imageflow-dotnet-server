using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

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
            "quality", "zoom", "dpr", "crop", "cropxunits", "cropyunits",
            "w", "h", "width", "height", "maxwidth", "maxheight", "format", "thumbnail",
            "autorotate", "srotate", "rotate", "ignoreicc",
            "stretch", "webp.lossless", "webp.quality",
            "frame", "page", "subsampling", "colors", "f.sharpen", "f.sharpen_when", "down.colorspace",
            "404", "bgcolor", "paddingcolor", "bordercolor", "preset", "floatspace", "jpeg_idct_downscale_linear", "watermark",
            "s.invert", "s.sepia", "s.grayscale", "s.alpha", "s.brightness", "s.contrast", "s.saturation", "trim.threshold",
            "trim.percentpadding", "a.blur", "a.sharpen", "a.removenoise", "a.balancewhite", "dither", "jpeg.progressive",
            "encoder", "decoder", "builder", "s.roundcorners.", "paddingwidth", "paddingheight", "margin", "borderwidth", "decoder.min_precise_scaling_ratio"
        };

        public static IEnumerable<string> AcceptedImageExtensions => suffixes;
        public static IEnumerable<string> SupportedQuerystringKeys => querystringKeys;

        internal static bool IsImagePath(PathString path)
        {
            if (path == null || !path.HasValue)
                return false;

            return suffixes.Any(suffix => path.Value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        internal static string SanitizeImageExtension(string extension)
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
       

        internal static IEnumerable<string> MatchingResizeQueryStringParameters(IQueryCollection queryCollection)
        {
            return querystringKeys
                .Where(queryCollection.ContainsKey)
                .Select(qsKey => qsKey + "=" + queryCollection[qsKey]);
        }

        internal static string Base64Hash(string data)
        {
            using var sha2 = SHA256.Create();
            var stringBytes = Encoding.UTF8.GetBytes(data);
            // check cache and return if cached
            var hashBytes =
                sha2.ComputeHash(stringBytes);
            return  Convert.ToBase64String(hashBytes)
                .Replace("=", string.Empty)
                .Replace('+', '-')
                .Replace('/', '_');
        }


        public static Dictionary<string, string> ToQueryDictionary(IQueryCollection requestQuery)
        {
            var dict = new Dictionary<string,string>(requestQuery.Count);
            foreach (var pair in requestQuery)
            {
                dict.Add(pair.Key, pair.Value.ToString());
            }

            return dict;
        }

        public static string SerializeCommandString(Dictionary<string, string> finalQuery)
        {
            var qs = QueryString.Create(finalQuery.Select(p => new KeyValuePair<string, StringValues>(p.Key, p.Value)));
            return qs.ToString() ?? "";
        }
    }
}