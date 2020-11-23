using Imazen.Common.Helpers;
using Imazen.Common.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Imageflow.Server.Storage.RemoteReader
{
    public class RemoteReaderService : IBlobProvider
    {

        private readonly List<string> prefixes = new List<string>();
        private readonly IHttpClientFactory httpFactory;
        private readonly RemoteReaderServiceOptions options;
        private readonly ILogger<RemoteReaderService> logger;

        private Func<Uri, string> HttpClientSelector;

        public RemoteReaderService(RemoteReaderServiceOptions options
            , Func<Uri, string> httpClientSelector
            , ILogger<RemoteReaderService> logger
            , IHttpClientFactory httpFactory
            ) : this(options, logger, httpFactory)
        {
            HttpClientSelector = httpClientSelector;
        }

        public RemoteReaderService(RemoteReaderServiceOptions options
            , ILogger<RemoteReaderService> logger
            , IHttpClientFactory httpFactory
            )
        {
            this.options = options;
            this.logger = logger;
            this.httpFactory = httpFactory;
            HttpClientSelector = (_) => "";

            prefixes.AddRange(this.options.Prefixes);
            prefixes.Sort((a, b) => b.Length.CompareTo(a.Length));

        }

        /// <summary>
        /// The remote URL and signature are encoded in the "file" part
        /// of the virtualPath parameter as follows:
        /// path/path/.../path/url_b64.hmac.ext
        /// </summary>
        /// <param name="virtualPath"></param>
        /// <returns></returns>
        public async Task<IBlobData> Fetch(string virtualPath)
        {
            var remote = virtualPath
                .Split('/')
                .Last()?
                .Split('.');

            if (remote == null || remote.Length < 2)
            {
                logger.LogWarning("Invalid remote path: {VirtualPath}", virtualPath);
                throw new BlobMissingException($"Invalid remote path: {virtualPath}");
            }

            var urlBase64 = remote[0];
            var hmac = remote[1];
            var sig = Signatures.SignString(urlBase64, options.SigningKey, 8);

            if (hmac != sig)
            {
                logger.LogWarning("Missing or Invalid signature on remote path: {VirtualPath}", virtualPath);
                throw new BlobMissingException($"Missing or Invalid signature on remote path: {virtualPath}");
            }

            var url = EncodingUtils.FromBase64UToString(urlBase64);

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                logger.LogWarning("RemoteReader blob \"{VirtualPath}\" not found. Invalid Uri: {Url}", virtualPath, url);
                throw new BlobMissingException($"RemoteReader blob \"{virtualPath}\" not found. Invalid Uri: {url}");
            }

            var httpClientName = HttpClientSelector(uri);
            using var http = httpFactory.CreateClient(httpClientName);

            try
            {
                var resp = await http.GetAsync(url);

                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogWarning("RemoteReader blob \"{VirtualPath}\" not found. The remote \"{Url}\" responded with status: {StatusCode}", virtualPath, url, resp.StatusCode);
                    throw new BlobMissingException($"RemoteReader blob \"{virtualPath}\" not found. The remote \"{url}\" responded with status: {resp.StatusCode}");
                }

                return new RemoteReaderBlob(resp);
            }
            catch (BlobMissingException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RemoteReader blob error retrieving \"{Url}\" for \"{VirtualPath}\".", url, virtualPath);
                throw new BlobMissingException($"RemoteReader blob error retrieving \"{url}\" for \"{virtualPath}\".", ex);
            }
        }

        public IEnumerable<string> GetPrefixes()
        {
            return prefixes;
        }

        public bool SupportsPath(string virtualPath)
        {
            return prefixes.Any(s => virtualPath.StartsWith(s,
                options.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        }
        
        public static string EncodeAndSignUrl(string url, string key)
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var extension = Path.GetExtension(path);
            var sanitizedExtension = PathHelpers.SanitizeImageExtension(extension)?? "jpg";
            var data = EncodingUtils.ToBase64U(url);
            var sig = Signatures.SignString(data, key,8);
            return $"{data}.{sig}.{sanitizedExtension}";
        }
    }
}
