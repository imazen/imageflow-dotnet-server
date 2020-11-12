using Imazen.Common.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Imazen.Common.Helpers;

namespace Imageflow.Server.Storage.RemoteReader
{
    public class RemoteReaderService : IBlobProvider
    {

        private readonly List<string> prefixes = new List<string>();
        private readonly HttpClient http;
        private readonly RemoteReaderServiceOptions options;
        // ReSharper disable once NotAccessedField.Local
        private readonly ILogger<RemoteReaderService> logger;

        public RemoteReaderService(RemoteReaderServiceOptions options, ILogger<RemoteReaderService> logger)
        {
            this.options = options;
            this.logger = logger;

            prefixes.AddRange(this.options.Prefixes);
            prefixes.Sort((a, b) => b.Length.CompareTo(a.Length));

            http = new HttpClient();
            http.DefaultRequestHeaders.Add("user-agent", this.options.UserAgent);
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
                .Last()
                .Split('.');

            var urlBase64 = remote[0];
            var hmac = remote[1];
            var sig =  Signatures.SignString(urlBase64, options.SigningKey,8);

            if (hmac != sig) 
                throw new BlobMissingException($"Missing or Invalid signature on remote path: {virtualPath}");

            var url = EncodingUtils.FromBase64UToString(urlBase64);

            var resp = await http.GetAsync(url);

            var redirectCount = 0;

            while (resp.StatusCode == System.Net.HttpStatusCode.Redirect 
                && redirectCount++ < options.RedirectLimit
                && resp.Headers.Location != null
                )
            {
                resp = await http.GetAsync(resp.Headers.Location);
            }
            return new RemoteReaderBlob(resp);
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
