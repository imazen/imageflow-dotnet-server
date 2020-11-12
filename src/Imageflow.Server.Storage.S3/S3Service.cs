using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Imazen.Common.Storage;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.Storage.S3
{
    public class S3Service : IBlobProvider
    {
        private readonly List<PrefixMapping> mappings = new List<PrefixMapping>();

        private readonly AWSCredentials credentials;
        public S3Service(S3ServiceOptions options, ILogger<S3Service> logger)
        {

            if (options.AccessKeyId == null)
            {
                credentials = new AnonymousAWSCredentials();
            }
            else
            {
                credentials = new  BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey);
            }

            foreach (var m in options.Mappings)
            {
                mappings.Add(m);;
            }
            //TODO: verify this sorts longest first
            mappings.Sort((a,b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
        }

        public IEnumerable<string> GetPrefixes()
        {
            return mappings.Select(m => m.Prefix);
        }

        public bool SupportsPath(string virtualPath)
        {
            return mappings.Any(m => virtualPath.StartsWith(m.Prefix, 
                m.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        }

        public async Task<IBlobData> Fetch(string virtualPath)
        {
            var mapping =  mappings.FirstOrDefault(m => virtualPath.StartsWith(m.Prefix, 
                m.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (mapping.Prefix == null)
            {
                return null;
            }

            var partialKey = virtualPath.Substring(mapping.Prefix.Length).TrimStart('/');
            if (mapping.LowercaseBlobPath)
            {
                partialKey = partialKey.ToLowerInvariant();
            }
           
            var key = string.IsNullOrEmpty(mapping.BlobPrefix)
                ? partialKey
                : mapping.BlobPrefix + "/" + partialKey;

            try
            {
                using var client = new AmazonS3Client(credentials, mapping.Config);
                var req = new Amazon.S3.Model.GetObjectRequest() { BucketName = mapping.Bucket, Key = key };

                var s = await client.GetObjectAsync(req);
                return new S3Blob(s);

            } catch (AmazonS3Exception se) {
                if (se.StatusCode == System.Net.HttpStatusCode.NotFound || "NoSuchKey".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                    throw new BlobMissingException($"Amazon S3 blob \"{key}\" not found.\n({se.Message})", se);
                if ( se.StatusCode == System.Net.HttpStatusCode.Forbidden || "AccessDenied".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                    throw new BlobMissingException($"Amazon S3 blob \"{key}\" not accessible. The blob may not exist or you may not have permission to access it.\n({se.Message})", se);
                throw;
            }
        }
    }
}