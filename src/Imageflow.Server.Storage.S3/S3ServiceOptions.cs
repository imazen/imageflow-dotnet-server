using System;
using System.Collections.Generic;
using Amazon.S3;
using Imageflow.Server.Storage.S3.Caching;

namespace Imageflow.Server.Storage.S3
{
    public class S3ServiceOptions
    {
        internal readonly List<PrefixMapping> Mappings = new List<PrefixMapping>();
        internal readonly List<NamedCacheConfiguration> NamedCaches = new List<NamedCacheConfiguration>();
        internal string UniqueName { get; private set; } = "s3";
        
        public S3ServiceOptions SetUniqueName(string uniqueName)
        {
            UniqueName = uniqueName;
            return this;
        }

        

        public S3ServiceOptions MapPrefix(string prefix, string bucket)
            => MapPrefix(prefix, bucket, "");

        public S3ServiceOptions MapPrefix(string prefix, string bucket, bool ignorePrefixCase,
            bool lowercaseBlobPath)
            => MapPrefix(prefix, bucket, "", ignorePrefixCase, lowercaseBlobPath);
        public S3ServiceOptions MapPrefix(string prefix, string bucket, string blobPrefix)
            => MapPrefix(prefix, bucket, blobPrefix, false, false);


        /// <summary>
        /// Maps a given prefix to a specified location within a bucket
        /// </summary>
        /// <param name="prefix">The prefix to capture image requests within</param>
        /// <param name="bucket">The bucket to serve images from</param>
        /// <param name="blobPrefix">The path within the bucket to serve images from. Can be an empty string to serve
        /// from root of bucket.</param>
        /// <param name="ignorePrefixCase">Whether to be cases sensitive about requests matching 'prefix'</param>
        /// <param name="lowercaseBlobPath">Whether to lowercase all incoming paths to allow for case insensitivity
        /// (requires that actual blobs all be lowercase).</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public S3ServiceOptions MapPrefix(string prefix, string bucket, string blobPrefix,
            bool ignorePrefixCase, bool lowercaseBlobPath)
            => MapPrefix(prefix, null, bucket, blobPrefix, ignorePrefixCase, lowercaseBlobPath);

        /// <summary>
        /// Maps a given prefix to a specified location within a bucket
        /// </summary>
        /// <param name="prefix">The prefix to capture image requests within</param>
        /// <param name="s3Client">The S3 client to use</param>
        /// <param name="bucket">The bucket to serve images from</param>
        /// <param name="blobPrefix">The path within the bucket to serve images from. Can be an empty string to serve
        /// from root of bucket.</param>
        /// <param name="ignorePrefixCase">Whether to be cases sensitive about requests matching 'prefix'</param>
        /// <param name="lowercaseBlobPath">Whether to lowercase all incoming paths to allow for case insensitivity
        /// (requires that actual blobs all be lowercase).</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public S3ServiceOptions MapPrefix(string prefix, IAmazonS3? s3Client, string bucket, string blobPrefix, bool ignorePrefixCase, bool lowercaseBlobPath)
        {
            prefix = prefix.TrimStart('/').TrimEnd('/');
            if (prefix.Length == 0)
            {
                throw new ArgumentException("Prefix cannot be /", nameof(prefix));
            }

            prefix = '/' + prefix + '/';
            blobPrefix = blobPrefix.Trim('/');

            Mappings.Add(new PrefixMapping
            {
                Bucket = bucket,
                Prefix = prefix,
                S3Client = s3Client,
                BlobPrefix = blobPrefix,
                IgnorePrefixCase = ignorePrefixCase,
                LowercaseBlobPath = lowercaseBlobPath

            });
            return this;
        }

        /// <summary>
        /// Adds a named cache location to the S3Service. This allows you to use the same S3Service instance to provide persistence for caching result images.
        /// You must also ensure that the bucket is located in the same AWS region as the Imageflow Server, that is uses S3 Standard, and that it is not publicly accessible. 
        /// </summary>
        /// <param name="namedCacheConfiguration"></param>
        /// <returns></returns>
        public S3ServiceOptions AddNamedCache(NamedCacheConfiguration namedCacheConfiguration)
        {
            NamedCaches.Add(namedCacheConfiguration);
            return this;
        }
    }
}