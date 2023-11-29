namespace Imageflow.Server
{
    public static class PathHelpers
    {
        public static IEnumerable<string> AcceptedImageExtensions => Imazen.Routing.Helpers.PathHelpers.AcceptedImageExtensions;
        public static IEnumerable<string> SupportedQuerystringKeys => Imazen.Routing.Helpers.PathHelpers.SupportedQuerystringKeys;
        internal static bool IsImagePath(string path) => Imazen.Routing.Helpers.PathHelpers.IsImagePath(path);
        public static string? SanitizeImageExtension(string extension) => Imazen.Routing.Helpers.PathHelpers.SanitizeImageExtension(extension);
        public static string? GetImageExtensionFromContentType(string contentType) => Imazen.Routing.Helpers.PathHelpers.GetImageExtensionFromContentType(contentType);


        /// <summary>
        /// Creates a 43-character URL-safe hash of arbitrary data. Good for implementing caches. SHA256 is used, and the result is base64 encoded with no padding, and the + and / characters replaced with - and _.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string CreateBase64UrlHash(string data) => Imazen.Routing.Helpers.PathHelpers.CreateBase64UrlHash(data);

        /// <summary>
        /// Creates a 43-character URL-safe hash of arbitrary data. Good for implementing caches. SHA256 is used, and the result is base64 encoded with no padding, and the + and / characters replaced with - and _.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string CreateBase64UrlHash(byte[] data) => Imazen.Routing.Helpers.PathHelpers.CreateBase64UrlHash(data);


    }
}