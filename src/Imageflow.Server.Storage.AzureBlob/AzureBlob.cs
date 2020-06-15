using System;
using System.IO;
using Azure;
using Azure.Storage.Blobs.Models;
using Imazen.Common.Storage;

namespace Imageflow.Server.Storage.AzureBlob
{
    internal class AzureBlob :IBlobData, IDisposable
    {
        private readonly Response<BlobDownloadInfo> response;

        internal AzureBlob(Response<BlobDownloadInfo> r)
        {
            response = r;
        }

        public bool? Exists => true;
        public DateTime? LastModifiedDateUtc => response.Value.Details.LastModified.UtcDateTime;
        public Stream OpenRead()
        {
            return response.Value.Content;
        }

        public void Dispose()
        {
            response?.Value?.Dispose();
        }
    }
}