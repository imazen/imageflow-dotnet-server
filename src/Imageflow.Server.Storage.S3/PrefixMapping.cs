using System;
using Amazon.S3;

namespace Imageflow.Server.Storage.S3
{
    internal struct PrefixMapping : IDisposable
    {
        internal string Prefix;
        internal IAmazonS3 S3Client;
        internal string Bucket;
        internal string BlobPrefix;
        internal bool IgnorePrefixCase;
        internal bool LowercaseBlobPath;
        public void Dispose()
        {
            try { S3Client?.Dispose(); } catch { }
        }
    }
}