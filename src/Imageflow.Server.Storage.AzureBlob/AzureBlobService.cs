using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Imazen.Common.Storage;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.Storage.AzureBlob
{
    public class AzureBlobService : IBlobProvider
    {
        private readonly Dictionary<string, PrefixMapping> mappings = new Dictionary<string, PrefixMapping>();

        private readonly List<string> prefixes = new List<string>();

        private readonly Azure.Storage.Blobs.BlobServiceClient client;

        public AzureBlobService(AzureBlobServiceOptions options, ILogger<AzureBlobService> logger)
        {
            client = new BlobServiceClient(options.ConnectionString, options.BlobClientOptions);
            foreach (var m in options.mappings)
            {
                mappings.Add(m.UrlPrefix, m);
                prefixes.Add(m.UrlPrefix);
            }

            prefixes.Sort((a, b) => a.Length.CompareTo(b.Length));
        }

        public IEnumerable<string> GetPrefixes()
        {
            return prefixes;
        }

        public bool SupportsPath(string virtualPath)
        {
            return prefixes.Any(s => virtualPath.StartsWith(s, StringComparison.Ordinal));
        }

        public async Task<IBlobData> Fetch(string virtualPath)
        {
            var prefix = prefixes.FirstOrDefault(s => virtualPath.StartsWith(s, StringComparison.Ordinal));
            if (prefix == null)
            {
                return null;
            }

            var mapping = mappings[prefix];
            
            var key = string.IsNullOrEmpty(mapping.BlobPrefix)
                    ? virtualPath.Substring(prefix.Length).TrimStart('/')
                    : mapping.BlobPrefix + "/" + virtualPath.Substring(prefix.Length).TrimStart('/');

            try
            {
                var blobClient = client.GetBlobContainerClient(mapping.Container).GetBlobClient(key);

                var s = await blobClient.DownloadAsync();
                return new AzureBlob(s);

            }
            catch (RequestFailedException e)
            {
                if (e.Status == 404)
                {
                    throw new FileNotFoundException("Azure blob file not found", e);
                }

                throw;

            }
        }
    }
}