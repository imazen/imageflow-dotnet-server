using System.Security.Cryptography;
using System.Text;
using Imazen.Abstractions.HttpStrings;
using Microsoft.Extensions.Primitives;

namespace Imazen.Routing.Helpers
{
    public static class PathHelpers
    {
        
        // ReSharper disable once HeapView.ObjectAllocation
        private static readonly string[] Suffixes = {
            ".png",
            ".jpg",
            ".jpeg",
            ".jfif",
            ".jif",
            ".jfi",
            ".jpe",
            ".gif",
            ".webp",
            // Soon, .avif and .jxl will be added
        };

        //TODO: Some of these are only modifier keys, and as such, should not trigger processing
        private static readonly string[] QuerystringKeys = new string[]
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

        public static IEnumerable<string> AcceptedImageExtensions => Suffixes;
        public static IEnumerable<string> SupportedQuerystringKeys => QuerystringKeys;
        
        internal static string[] ImagePathSuffixes => Suffixes;
        internal static bool IsImagePath(string path)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var suffix in Suffixes)
            {
                if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static string? SanitizeImageExtension(string extension)
        {
            extension = extension.ToLowerInvariant().TrimStart('.');
            return extension switch
            {
                "png" => "png",
                "gif" => "gif",
                "webp" => "webp",
                "jpeg" => "jpg",
                "jfif" => "jpg",
                "jif" => "jpg",
                "jfi" => "jpg",
                "jpe" => "jpg",
                _ => null 
            };
        }
        public static string? GetImageExtensionFromContentType(string? contentType)
        {
            return contentType switch
            {
                "image/png" => "png",
                "image/gif" => "gif",
                "image/webp" => "webp",
                "image/jpeg" => "jpg",
                _ => null 
            };
        }


        /// <summary>
        /// Creates a 43-character URL-safe hash of arbitrary data. Good for implementing caches. SHA256 is used, and the result is base64 encoded with no padding, and the + and / characters replaced with - and _.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string CreateBase64UrlHash(string data) => CreateBase64UrlHash(Encoding.UTF8.GetBytes(data));

        /// <summary>
        /// Creates a 43-character URL-safe hash of arbitrary data. Good for implementing caches. SHA256 is used, and the result is base64 encoded with no padding, and the + and / characters replaced with - and _.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string CreateBase64UrlHash(byte[] data){
            using var sha2 = SHA256.Create();
            // check cache and return if cached
            var hashBytes =
                sha2.ComputeHash(data);
            return  Convert.ToBase64String(hashBytes)
                .Replace("=", string.Empty)
                .Replace('+', '-')
                .Replace('/', '_');
        }


        internal static Dictionary<string, string> ToQueryDictionary(IEnumerable<KeyValuePair<string,StringValues>> requestQuery)
        {
            var dict = new Dictionary<string,string>(8, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in requestQuery)
            {
                dict.Add(pair.Key, pair.Value.ToString());
            }

            return dict;
        }

        internal static string SerializeCommandString(Dictionary<string, string> finalQuery)
        {
            var qs = UrlQueryString.Create(finalQuery.Select(p => new KeyValuePair<string, StringValues>(p.Key, p.Value)));
            return qs.ToString()?.TrimStart('?') ?? "";
        }

        public static Dictionary<string, StringValues>? ParseQuery(string querystring)
        {
            return QueryHelpers.ParseNullableQuery(querystring);
        }
    }
}