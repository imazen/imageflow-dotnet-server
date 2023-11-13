
using System;
using Amazon.S3;

namespace Imageflow.Server.Storage.S3.Caching
{
    /// <summary>
    /// An immutable data structure that holds two strings,  Bucket, and BlobPrefix. 
    /// BlobPrefix is the prefix of the blob key, not including the bucket name. It cannot start with a slash, and must be a valid S3 blob string
    /// Bucket must be a valid S3 bucket name. Use StringValidator
    /// </summary>
    internal readonly struct BlobGroupLocation: IDisposable
    {
        internal readonly string BucketName;
        internal readonly string BlobPrefix;
        internal readonly IAmazonS3 S3Client;

        internal BlobGroupLocation(string bucketName, string blobPrefix, IAmazonS3 s3Client)
        {
            if (!StringValidator.ValidateBucketName(bucketName, out var error))
            {
                throw new ArgumentException(error, nameof(bucketName));
            }
            if (!StringValidator.ValidateKeyPrefix(blobPrefix, out error))
            {
                throw new ArgumentException(error, nameof(blobPrefix));
            }
            BucketName = bucketName;
            BlobPrefix = blobPrefix;
            S3Client = s3Client;
        }

        public void Dispose()
        {
            S3Client.Dispose();
        }
    }
}