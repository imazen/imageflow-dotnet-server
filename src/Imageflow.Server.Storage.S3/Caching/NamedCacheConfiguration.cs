using System;
using System.Collections.Generic;
using Amazon.S3;
using Imazen.Abstractions.BlobCache;

namespace Imageflow.Server.Storage.S3.Caching
{
    internal enum KeyTransform{
        Identity
    }
    public readonly struct NamedCacheConfiguration : IDisposable
    {

        internal readonly string CacheName;

        internal readonly KeyTransform KeyTransform;
        internal readonly IAmazonS3 DefaultS3Client;
        internal readonly Dictionary<BlobGroup, BlobGroupConfiguration> BlobGroupConfigurations;


        /// <summary>
        /// Creates a new named cache configuration with the specified blob group configurations
        /// </summary>
        /// <param name="cacheName"></param>
        /// <param name="defaultS3Client"></param>
        /// <param name="blobGroupConfigurations"></param>
        internal NamedCacheConfiguration(string cacheName, IAmazonS3 defaultS3Client, Dictionary<BlobGroup, BlobGroupConfiguration> blobGroupConfigurations)
        {
            CacheName = cacheName;
            DefaultS3Client = defaultS3Client;
            BlobGroupConfigurations = blobGroupConfigurations;
            KeyTransform = KeyTransform.Identity;
        }


        /// <summary>
        /// Creates a new named cache configuration with the specified sliding expiry days
        /// </summary>
        /// <param name="cacheName">The unique name of the cache, for use in the rest of the configuration</param>
        /// <param name="defaultS3Client">If null, will use the default client.</param>
        /// <param name="cacheBucketName">The bucket to use for the cache. Must be in the same region or performance will be terrible.</param>
        /// <param name="slidingExpiryDays">If null, then no expiry will occur</param>
        /// <param name="createBucketIfMissing">If true, missing buckets will be created in the default region for the S3 client</param>
        /// <param name="updateLifecycleRules">If true, lifecycle rules will be synchronized.</param>
        /// <exception cref="ArgumentException"></exception>
        public NamedCacheConfiguration(string cacheName, IAmazonS3 defaultS3Client, string cacheBucketName, CacheBucketCreation createIfMissing, CacheBucketLifecycleRules updateLifecycleRule, int? slidingExpiryDays)
        {
            // slidingExpiryDays cannot be less than 3, if specified 
            if (slidingExpiryDays.HasValue && slidingExpiryDays.Value < 3)
            {
                throw new ArgumentException("slidingExpiryDays cannot be less than 3, if specified", nameof(slidingExpiryDays));
            }
            var blobMetadataExpiry = slidingExpiryDays.HasValue ? slidingExpiryDays.Value * 4 : (int?)null;
            var createBucketIfMissing = createIfMissing == CacheBucketCreation.CreateIfMissing;
            var updateLifecycleRules = updateLifecycleRule == CacheBucketLifecycleRules.ConfigureExpiryForCacheFolders;

            KeyTransform = KeyTransform.Identity;
            CacheName = cacheName;
            DefaultS3Client = defaultS3Client;
            BlobGroupConfigurations = new Dictionary<BlobGroup, BlobGroupConfiguration>
            {
                { BlobGroup.GeneratedCacheEntry, new BlobGroupConfiguration(new BlobGroupLocation(cacheBucketName, "imageflow-cache/blobs/files/", "imageflow-cache/blobs/by-tags/",defaultS3Client), new BlobGroupLifecycle(slidingExpiryDays, slidingExpiryDays), createBucketIfMissing, updateLifecycleRules, false) },
                { BlobGroup.SourceMetadata, new BlobGroupConfiguration(new BlobGroupLocation(cacheBucketName, "imageflow-cache/source-metadata/files/", "imageflow-cache/source-metadata/by-tags/", defaultS3Client), new BlobGroupLifecycle(null, null), createBucketIfMissing, updateLifecycleRules, false ) },
                { BlobGroup.CacheEntryMetadata, new BlobGroupConfiguration(new BlobGroupLocation(cacheBucketName, "imageflow-cache/blob-metadata/files/", "imageflow-cache/blob-metadata/by-tags/",defaultS3Client), new BlobGroupLifecycle(blobMetadataExpiry, blobMetadataExpiry), createBucketIfMissing, updateLifecycleRules, false) },
                { BlobGroup.Essential, new BlobGroupConfiguration(new BlobGroupLocation(cacheBucketName, "imageflow-cache/essential/files/", "imageflow-cache/essential/by-tags/",defaultS3Client), new BlobGroupLifecycle(null, null), createBucketIfMissing, updateLifecycleRules, false) }
            };
        }

        public void Dispose()
        {
            try { DefaultS3Client?.Dispose(); } catch { }
        }
    }
}