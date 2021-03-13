using System;
using System.Collections.Generic;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;

namespace Imageflow.Server.Storage.S3
{
    public class S3ServiceOptions
    {
        private readonly AWSCredentials credentials;
        internal readonly List<PrefixMapping> Mappings = new List<PrefixMapping>();

        public S3ServiceOptions()
        {
            credentials = new AnonymousAWSCredentials();
        }

        public S3ServiceOptions(AWSCredentials credentials)
        {
            this.credentials = credentials;
        }

        public S3ServiceOptions(string accessKeyId, string secretAccessKey)
        {
            credentials = accessKeyId == null
                ? (AWSCredentials) new AnonymousAWSCredentials()
                : new BasicAWSCredentials(accessKeyId, secretAccessKey);
        }

        public S3ServiceOptions MapPrefix(string prefix, RegionEndpoint region, string bucket)
            => MapPrefix(prefix, region, bucket, "");

        public S3ServiceOptions MapPrefix(string prefix, RegionEndpoint region, string bucket, bool ignorePrefixCase,
            bool lowercaseBlobPath)
            => MapPrefix(prefix, region, bucket, "", ignorePrefixCase, lowercaseBlobPath);
        public S3ServiceOptions MapPrefix(string prefix, RegionEndpoint region, string bucket, string blobPrefix)
            => MapPrefix(prefix, region, bucket, blobPrefix, false, false);

        public S3ServiceOptions MapPrefix(string prefix, RegionEndpoint region, string bucket, string blobPrefix,
            bool ignorePrefixCase, bool lowercaseBlobPath)
            => MapPrefix(prefix, new AmazonS3Config() { RegionEndpoint = region }, bucket,
                blobPrefix, ignorePrefixCase, lowercaseBlobPath);

        /// <summary>
        /// Maps a given prefix to a specified location within a bucket
        /// </summary>
        /// <param name="prefix">The prefix to capture image requests within</param>
        /// <param name="s3Config">The configuration (such as region endpoint or service URL, etc) to use</param>
        /// <param name="bucket">The bucket to serve images from</param>
        /// <param name="blobPrefix">The path within the bucket to serve images from. Can be an empty string to serve
        /// from root of bucket.</param>
        /// <param name="ignorePrefixCase">Whether to be cases sensitive about requests matching 'prefix'</param>
        /// <param name="lowercaseBlobPath">Whether to lowercase all incoming paths to allow for case insensitivity
        /// (requires that actual blobs all be lowercase).</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public S3ServiceOptions MapPrefix(string prefix, AmazonS3Config s3Config, string bucket, string blobPrefix,
            bool ignorePrefixCase, bool lowercaseBlobPath)
        {
            Func<IAmazonS3> client = () => new AmazonS3Client(credentials, s3Config);
            return MapPrefix(prefix, client, bucket, blobPrefix, ignorePrefixCase, lowercaseBlobPath);
        }

        /// <summary>
        /// Maps a given prefix to a specified location within a bucket
        /// </summary>
        /// <param name="prefix">The prefix to capture image requests within</param>
        /// <param name="s3ClientFactory">Lambda function to provide an instance of IAmazonS3, which will be disposed after use.</param>
        /// <param name="bucket">The bucket to serve images from</param>
        /// <param name="blobPrefix">The path within the bucket to serve images from. Can be an empty string to serve
        /// from root of bucket.</param>
        /// <param name="ignorePrefixCase">Whether to be cases sensitive about requests matching 'prefix'</param>
        /// <param name="lowercaseBlobPath">Whether to lowercase all incoming paths to allow for case insensitivity
        /// (requires that actual blobs all be lowercase).</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public S3ServiceOptions MapPrefix(string prefix, Func<IAmazonS3> s3ClientFactory, string bucket, string blobPrefix, bool ignorePrefixCase, bool lowercaseBlobPath)
        {
            prefix = prefix.TrimStart('/').TrimEnd('/');
            if (prefix.Length == 0)
            {
                throw new ArgumentException("Prefix cannot be /", nameof(prefix));
            }

            prefix = '/' + prefix + '/';
            blobPrefix = blobPrefix.Trim('/');

            Mappings.Add(new PrefixMapping()
            {
                Bucket = bucket,
                Prefix = prefix,
                ClientFactory = s3ClientFactory,
                BlobPrefix = blobPrefix,
                IgnorePrefixCase = ignorePrefixCase,
                LowercaseBlobPath = lowercaseBlobPath

            });
            return this;
        }

    }
}