using System;
using System.Collections.Generic;
using Azure.Storage.Blobs;

namespace Imageflow.Server.Storage.AzureBlob
{
    public class AzureBlobServiceOptions
    {
        public BlobClientOptions BlobClientOptions { get; set; }
        public string ConnectionString { get; set; }

        internal readonly List<PrefixMapping> Mappings = new List<PrefixMapping>();
        
        public AzureBlobServiceOptions(string connectionString, BlobClientOptions blobClientOptions = null)
        {
            BlobClientOptions = blobClientOptions ?? new BlobClientOptions();
            ConnectionString = connectionString;
        }

        public AzureBlobServiceOptions MapPrefix(string urlPrefix, string container)
            => MapPrefix(urlPrefix, container, "");

        public AzureBlobServiceOptions MapPrefix(string urlPrefix, string container, bool ignorePrefixCase, bool lowercaseBlobPath)
            => MapPrefix(urlPrefix, container, "", ignorePrefixCase, lowercaseBlobPath);
            
        public AzureBlobServiceOptions MapPrefix(string urlPrefix, string container, string blobPrefix)
            => MapPrefix(urlPrefix, container, blobPrefix, false, false);
        public AzureBlobServiceOptions MapPrefix(string urlPrefix, string container, string blobPrefix, bool ignorePrefixCase, bool lowercaseBlobPath)
        {
            var prefix = urlPrefix.TrimStart('/').TrimEnd('/');
            if (prefix.Length == 0)
            {
                throw new ArgumentException("Prefix cannot be /", nameof(prefix));
            }

            prefix = '/' + prefix + '/';

            blobPrefix = blobPrefix.Trim('/');


            Mappings.Add(new PrefixMapping()
            {
                Container = container, 
                BlobPrefix = blobPrefix, 
                UrlPrefix = prefix, 
                IgnorePrefixCase = ignorePrefixCase,
                LowercaseBlobPath = lowercaseBlobPath
            });
            return this;
        }
    }
}