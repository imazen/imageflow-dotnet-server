using Imageflow.Server.ServerHelpers;
using Imazen.Common.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Imageflow.Server.Storage.RemoteReader
{
    public class RemoteReaderService : IBlobProvider
    {

        private readonly List<string> prefixes = new List<string>();
        private readonly HttpClient _http;
        private readonly RemoteReaderServiceOptions _options;
        private readonly ILogger<RemoteReaderService> _logger;

        public RemoteReaderService(RemoteReaderServiceOptions options, ILogger<RemoteReaderService> logger)
        {
            _options = options;
            _logger = logger;

            prefixes.AddRange(_options._prefixes);
            prefixes.Sort((a, b) => b.Length.CompareTo(a.Length));

            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("user-agent", _options.UserAgent);
        }

        /// <summary>
        /// The remote URL and signature are encoded in the "file" part
        /// of the virtualPath parameter as follows:
        /// path/path/.../path/urlb64.hmac.ext
        /// </summary>
        /// <param name="virtualPath"></param>
        /// <returns></returns>
        public async Task<IBlobData> Fetch(string virtualPath)
        {
            var remote = virtualPath
                .Split('/')
                .Last()
                .Split('.');

            var urlb64 = remote[0];
            var hmac = remote[1];
            var sig = SignData(urlb64, _options.SigningKey);

            if (hmac != sig) 
                throw new BlobMissingException($"Missing or Invalid signature on remote path: {virtualPath}");

            var url = EncodingUtils.FromBase64UToString(urlb64);

            var resp = await _http.GetAsync(url);
            return new RemoteReaderBlob(resp);
        }

        public IEnumerable<string> GetPrefixes()
        {
            return prefixes;
        }

        public bool SupportsPath(string virtualPath)
        {
            return prefixes.Any(s => virtualPath.StartsWith(s,
                _options.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        }

        public static string SignData(string data, string key)
        {
            HMACSHA256 hmac = new HMACSHA256(UTF8Encoding.UTF8.GetBytes(key));
            byte[] hash = hmac.ComputeHash(UTF8Encoding.UTF8.GetBytes(data));
            //32-byte hash is a bit overkill. Truncation only marginally weakens the algorithm integrity.
            byte[] shorterHash = new byte[8];
            Array.Copy(hash, shorterHash, 8);
            return EncodingUtils.ToBase64U(shorterHash);
        }
        public static string EncodeAndSignUrl(string url, string key)
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var extension = Path.GetExtension(path);
            var sanitizedExtension = PathHelpers.SanitizeImageExtension(extension);
            var data = EncodingUtils.ToBase64U(url);
            var sig = SignData(data, key);
            return $"{data}.{sig}.{sanitizedExtension}";
        }


    }
}
