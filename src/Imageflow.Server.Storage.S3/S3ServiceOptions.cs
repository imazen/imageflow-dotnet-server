using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using Amazon.S3;

namespace Imageflow.Server.Storage.S3
{
    public class S3ServiceOptions
    {

        internal readonly string AccessKeyId;
        internal readonly string SecretAccessKey;
        internal readonly List<PrefixMapping> Mappings = new List<PrefixMapping>();
        public S3ServiceOptions(string accessKeyId, string secretAccessKey)
        {
            this.AccessKeyId = accessKeyId;
            this.SecretAccessKey = secretAccessKey;
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
            => MapPrefix(prefix, new AmazonS3Config() {RegionEndpoint = region}, bucket,
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
        public S3ServiceOptions MapPrefix(string prefix, AmazonS3Config s3Config, string bucket, string blobPrefix, bool ignorePrefixCase, bool lowercaseBlobPath)
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
                Bucket=bucket, 
                Prefix=prefix, 
                Config=s3Config, 
                BlobPrefix = blobPrefix,
                IgnorePrefixCase = ignorePrefixCase,
                LowercaseBlobPath = lowercaseBlobPath
                
            });
            return this;
        }
        
    }
}