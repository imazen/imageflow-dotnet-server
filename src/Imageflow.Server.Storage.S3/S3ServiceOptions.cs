using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Amazon;

namespace Imageflow.Server.Storage.S3
{
    public class S3ServiceOptions
    {

        internal readonly string AccessKeyId;
        internal readonly string SecretAccessKey;
        internal readonly RegionEndpoint DefaultRegion;
        internal readonly List<PrefixMapping> mappings = new List<PrefixMapping>();
        public S3ServiceOptions( RegionEndpoint defaultRegion, string accessKeyId, string secretAccessKey)
        {
            DefaultRegion = defaultRegion;
            this.AccessKeyId = accessKeyId;
            this.SecretAccessKey = secretAccessKey;
        }

        public S3ServiceOptions MapPrefix(string prefix, string region, string bucket)
            => MapPrefix(prefix, region, bucket, "");
        
        public S3ServiceOptions MapPrefix(string prefix, string region, string bucket, string blobPrefix)
        {
            prefix = prefix.TrimStart('/').TrimEnd('/');
            if (prefix.Length == 0)
            {
                throw new ArgumentException("Prefix cannot be /", nameof(prefix));
            }

            prefix = '/' + prefix + '/';
            blobPrefix = blobPrefix.Trim('/');

            mappings.Add(new PrefixMapping() {Bucket=bucket, Prefix=prefix, Region=region, BlobPrefix = blobPrefix});
            return this;
        }
        
    }
}