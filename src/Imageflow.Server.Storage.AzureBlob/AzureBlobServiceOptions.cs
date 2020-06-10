using System;
using System.Collections.Generic;
using Azure.Storage.Blobs;

namespace Imageflow.Server.Storage.AzureBlob
{
    public class AzureBlobServiceOptions
    {
        public Azure.Storage.Blobs.BlobClientOptions BlobClientOptions { get; set; }
        public string ConnectionString { get; set; }

        internal readonly List<PrefixMapping> mappings = new List<PrefixMapping>();
        
        public AzureBlobServiceOptions(string connectionString, BlobClientOptions blobClientOptions = null)
        {
            BlobClientOptions = blobClientOptions ?? new BlobClientOptions();
            ConnectionString = connectionString;
        }


        public AzureBlobServiceOptions MapPrefix(string urlPrefix, string container, string blobPrefix = "")
        {
            var prefix = urlPrefix.TrimStart('/').TrimEnd('/');
            if (prefix.Length == 0)
            {
                throw new ArgumentException("Prefix cannot be /", nameof(prefix));
            }

            prefix = '/' + prefix + '/';


            mappings.Add(new PrefixMapping()
            {
                Container = container, BlobPrefix = blobPrefix, UrlPrefix = prefix
            });
            return this;
        }
    }
}