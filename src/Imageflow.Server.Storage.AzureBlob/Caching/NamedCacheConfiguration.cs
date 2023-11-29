using System;
using System.Collections.Generic;
using Imazen.Abstractions.BlobCache;

namespace Imageflow.Server.Storage.AzureBlob.Caching
{
    internal enum KeyTransform{
        Identity
    }
    public readonly struct NamedCacheConfiguration 
    {

        internal readonly string CacheName;

        internal readonly KeyTransform KeyTransform;
        internal readonly BlobClientOrName? BlobClient;
        internal readonly Dictionary<BlobGroup, BlobGroupConfiguration> BlobGroupConfigurations;


        /// <summary>
        /// Creates a new named cache configuration with the specified blob group configurations
        /// </summary>
        /// <param name="cacheName"></param>
        /// <param name="defaultS3Client"></param>
        /// <param name="blobGroupConfigurations"></param>
        internal NamedCacheConfiguration(string cacheName, BlobClientOrName defaultClient, Dictionary<BlobGroup, BlobGroupConfiguration> blobGroupConfigurations)
        {
            CacheName = cacheName;
            BlobClient = defaultClient;
            BlobGroupConfigurations = blobGroupConfigurations;
            KeyTransform = KeyTransform.Identity;
        }


        /// <summary>
        /// Creates a new named cache configuration with the specified sliding expiry days
        /// </summary>
        /// <param name="cacheName">The name of the cache, for use in the rest of the configuration</param>
        /// <param name="defaultClient">If null, will use the default client.</param>
        /// <param name="cacheBucketName">The bucket to use for the cache. Must be in the same region or performance will be terrible.</param>
        /// <param name="slidingExpiryDays">If null, then no expiry will occur</param>
        /// <param name="createBucketIfMissing">If true, missing buckets will be created in the default region for the S3 client</param>
        /// <param name="updateLifecycleRules">If true, lifecycle rules will be synchronized.</param>
        /// <exception cref="ArgumentException"></exception>
        internal NamedCacheConfiguration(string cacheName, BlobClientOrName defaultClient, string cacheBucketName, CacheBucketCreation createIfMissing, CacheBucketLifecycleRules updateLifecycleRule, int? slidingExpiryDays)
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
            BlobClient = defaultClient;
            BlobGroupConfigurations = new Dictionary<BlobGroup, BlobGroupConfiguration>
            {
                { BlobGroup.GeneratedCacheEntry, new BlobGroupConfiguration(new BlobGroupLocation(cacheBucketName, "imageflow-cache/blobs/", defaultClient), BlobGroupLifecycle.SlidingExpiry(slidingExpiryDays), createBucketIfMissing, updateLifecycleRules) },
                { BlobGroup.SourceMetadata, new BlobGroupConfiguration(new BlobGroupLocation(cacheBucketName, "imageflow-cache/source-metadata/", defaultClient), BlobGroupLifecycle.NonExpiring, createBucketIfMissing, updateLifecycleRules) },
                { BlobGroup.CacheEntryMetadata, new BlobGroupConfiguration(new BlobGroupLocation(cacheBucketName, "imageflow-cache/blob-metadata/", defaultClient), BlobGroupLifecycle.SlidingExpiry(blobMetadataExpiry), createBucketIfMissing, updateLifecycleRules) },
                { BlobGroup.Essential, new BlobGroupConfiguration(new BlobGroupLocation(cacheBucketName, "imageflow-cache/essential/", defaultClient), BlobGroupLifecycle.NonExpiring, createBucketIfMissing, updateLifecycleRules) }
            };
        }

        /// <summary>
        /// Creates a new named cache configuration with the specified cache name and bucket name and createIfMissing (no expiry)
        /// </summary>
        /// <param name="cacheName">The name of the cache, for use in the rest of the configuration</param>
        /// <param name="defaultClient">If null, will use the default client.</param>
        /// <param name="cacheBucketName">The bucket to use for the cache. Must be in the same region or performance will be terrible.</param>
        /// <param name="createBucketIfMissing">If true, missing buckets will be created in the default region for the Azure client</param>
        internal NamedCacheConfiguration(string cacheName, BlobClientOrName defaultClient, string cacheBucketName, CacheBucketCreation createIfMissing)
            : this(cacheName, defaultClient, cacheBucketName, createIfMissing, CacheBucketLifecycleRules.DoNotUpdate, null)
        {
        }
        /// <summary>
        /// Creates a new named cache configuration with the specified cache name and bucket name and createIfMissing (no expiry)
        /// </summary>
        /// <param name="cacheName">The name of the cache, for use in the rest of the configuration</param>
        /// <param name="defaultClient">If null, will use the default client.</param>
        /// <param name="cacheBucketName">The bucket to use for the cache. Must be in the same region or performance will be terrible.</param>
        /// <param name="createBucketIfMissing">If true, missing buckets will be created in the default region for the Azure client</param>
        public NamedCacheConfiguration(string cacheName, string cacheBucketName, string blobClientName, CacheBucketCreation createIfMissing)
            : this(cacheName, new BlobClientOrName(blobClientName), cacheBucketName, createIfMissing, CacheBucketLifecycleRules.DoNotUpdate, null)
        {
        }

    }
}