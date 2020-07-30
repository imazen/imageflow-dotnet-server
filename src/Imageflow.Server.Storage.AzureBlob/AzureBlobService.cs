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
        private readonly List<PrefixMapping> mappings = new List<PrefixMapping>();

        private readonly Azure.Storage.Blobs.BlobServiceClient client;

        public AzureBlobService(AzureBlobServiceOptions options, ILogger<AzureBlobService> logger)
        {
            client = new BlobServiceClient(options.ConnectionString, options.BlobClientOptions);
            foreach (var m in options.mappings)
            {
                mappings.Add(m);
            }

            mappings.Sort((a, b) => b.UrlPrefix.Length.CompareTo(a.UrlPrefix.Length));
        }

        public IEnumerable<string> GetPrefixes()
        {
            return mappings.Select(m => m.UrlPrefix);
        }

        public bool SupportsPath(string virtualPath)
        {
            return mappings.Any(s => virtualPath.StartsWith(s.UrlPrefix, 
                s.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        }

        public async Task<IBlobData> Fetch(string virtualPath)
        {
            var mapping = mappings.FirstOrDefault(s => virtualPath.StartsWith(s.UrlPrefix, 
                s.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (mapping.UrlPrefix == null)
            {
                return null;
            }

            var partialKey = virtualPath.Substring(mapping.UrlPrefix.Length).TrimStart('/');

            if (mapping.LowercaseBlobPath)
            {
                partialKey = partialKey.ToLowerInvariant();
            }

            var key = string.IsNullOrEmpty(mapping.BlobPrefix)
                    ? partialKey
                    : mapping.BlobPrefix + "/" + partialKey;

            
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
                    throw new BlobMissingException($"Azure blob \"{key}\" not found.", e);
                }

                throw;

            }
        }
    }
}