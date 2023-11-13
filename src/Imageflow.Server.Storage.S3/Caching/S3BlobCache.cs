using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Imageflow.Server.Storage.S3.Caching;
using Imazen.Common.Storage;
using Imazen.Common.Storage.Caching;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.Storage.S3.Caching{

    internal class S3BlobCache :IBlobCache
    {
        private NamedCacheConfiguration m;
        private IAmazonS3 s3client;
        private ILogger<S3Service> logger;

        public S3BlobCache(NamedCacheConfiguration m, IAmazonS3 s3client, ILogger<S3Service> logger)
        {
            this.m = m;
            this.s3client = s3client;
            this.logger = logger;
        }

        public string Name => m.CacheName;

        internal BlobGroupConfiguration GetConfigFor(BlobGroup group)
        {
            if (m.BlobGroupConfigurations.TryGetValue(group, out var groupConfig))
            {
                return groupConfig;
            }
            throw new Exception($"No configuration for blob group {group} in cache {Name}");
        }

        internal string TransformKey(string key)
        {
            switch (m.KeyTransform)
            {
                case KeyTransform.Identity:
                    return key;
                default:
                    throw new Exception($"Unknown key transform {m.KeyTransform}");
            }
        }

        internal string GetKeyFor(BlobGroup group, string key)
        {
            var groupConfig = GetConfigFor(group);
            return groupConfig.Location.BlobPrefix + TransformKey(key); // We don't add an extension because we can't verify the first few bytes.
        }

        
        /// <summary>
        /// This method may or may not have worth. If the index engine is external, it is more likely to know. 
        /// </summary>
        /// <param name="group"></param>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<bool> MayExist(BlobGroup group, string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public async Task<ICacheBlobPutResult> Put(BlobGroup group, string key, IBlobData data, ICacheBlobPutOptions options, CancellationToken cancellationToken = default)
        {
            var groupConfig = GetConfigFor(group);
            var s3key = GetKeyFor(group, key);
            var bucket = groupConfig.Location.BucketName;
            var client = groupConfig.Location.S3Client ?? s3client;
            var req = new Amazon.S3.Model.PutObjectRequest() { BucketName = bucket, Key = s3key, InputStream = data.OpenRead(), ContentType = "application/octet-stream" };
            try{
                var response = await client.PutObjectAsync(req);
                return new CacheBlobPutResult((int)response.HttpStatusCode, null, true);
            } catch (AmazonS3Exception se) {
                LogIfSerious(se, bucket, key);
                if ("NoSuchBucket".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                    return new CacheBlobPutResult((int)se.StatusCode, se.Message, false);
                if (se.StatusCode == System.Net.HttpStatusCode.Forbidden || "AccessDenied".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase))
                    return new CacheBlobPutResult((int)se.StatusCode, se.Message, false);
                throw;
            }
        }

        public async Task<ICacheBlobFetchResult> TryFetchBlob(BlobGroup group, string key, CancellationToken cancellationToken = default)
        {
            var groupConfig = GetConfigFor(group);
            var s3key = GetKeyFor(group, key);
            var bucket = groupConfig.Location.BucketName;
            var client = groupConfig.Location.S3Client ?? s3client;
            var req = new Amazon.S3.Model.GetObjectRequest() { BucketName = bucket, Key = s3key };
            try{
                var response = await client.GetObjectAsync(req);
                // We need to determine if the response successfully fetched the blob or not.
                bool success = response.HttpStatusCode == System.Net.HttpStatusCode.OK;
     
                return new CacheBlobFetchResult(new S3Blob(response),response.HttpStatusCode == System.Net.HttpStatusCode.OK, (int)response.HttpStatusCode, null);
            } catch (AmazonS3Exception se) {
                LogIfSerious(se, bucket, key);
                //For cache misses, just return a null blob. 
                if ("NoSuchBucket".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                    return new CacheBlobFetchResult(null,false, (int)se.StatusCode, se.Message);
                if (se.StatusCode == System.Net.HttpStatusCode.NotFound || "NoSuchKey".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                    return new CacheBlobFetchResult(null,false, (int)se.StatusCode, se.Message);
                if (se.StatusCode == System.Net.HttpStatusCode.Forbidden || "AccessDenied".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                    return new CacheBlobFetchResult(null,false, (int)se.StatusCode, se.Message);
                throw;
            }
        }

        internal void LogIfSerious(AmazonS3Exception se, string bucketName, string key){
            // if the bucket is missing, it's notable. 
            if ("NoSuchBucket".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                logger.LogError(se, $"Amazon S3 bucket \"{bucketName}\" not found. The bucket may not exist or you may not have permission to access it.\n({se.Message})");
            
            // if there's an authorization or authentication error, it's notable
            if (se.StatusCode == System.Net.HttpStatusCode.Forbidden || "AccessDenied".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                logger.LogError(se, $"Amazon S3 blob \"{key}\" in bucket \"{bucketName}\" not accessible. The blob may not exist or you may not have permission to access it.\n({se.Message})");

            // InvalidAccessKeyId and InvalidSecurity
            if ("InvalidAccessKeyId".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase) || "InvalidSecurity".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                logger.LogError(se, $"Your S3 credentials are not working. Caching to bucket \"{bucketName}\" is not operational.\n({se.Message})");
        }
    }
}