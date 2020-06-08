using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Imageflow.Server.Storage.S3
{
    public class S3ServiceSettings
    {

        internal readonly string AccessKeyId;
        internal readonly string SecretAccessKey;
        internal readonly List<PrefixMapping> mappings = new List<PrefixMapping>();
        public S3ServiceSettings(string accessKeyId, string secretAccessKey)
        {
            this.AccessKeyId = accessKeyId;
            this.SecretAccessKey = secretAccessKey;
        }

        public S3ServiceSettings MapPrefix(string prefix, string region, string bucket)
        {
            prefix = prefix.TrimStart('/').TrimEnd('/');
            if (prefix.Length == 0)
            {
                throw new ArgumentException("Prefix cannot be /", nameof(prefix));
            }

            prefix = '/' + prefix + '/';
            

            mappings.Add(new PrefixMapping() {Bucket=bucket, Prefix=prefix, Region=region});
            return this;
        }
        
    }
}