using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Imazen.Common.Storage;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.Storage.S3
{
    public class S3Service : IBlobProvider
    {
        private readonly Dictionary<string, PrefixMapping> mappings = new Dictionary<string, PrefixMapping>();
        
        private readonly List<string> prefixes = new List<string>();

        private readonly AmazonS3Client client;
        public S3Service(S3ServiceOptions options, ILogger<S3Service> logger)
        {

            if (options.AccessKeyId == null)
            {
                client = new AmazonS3Client(new AnonymousAWSCredentials(),options.DefaultRegion);
            }
            else
            {
                client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey));
            }

            foreach (var m in options.mappings)
            {
                mappings.Add(m.Prefix, m);
                prefixes.Add(m.Prefix);
            }
            //TODO: verify this sorts longest first
            prefixes.Sort((a,b) => a.Length.CompareTo(b.Length));
        }

        public IEnumerable<string> GetPrefixes()
        {
            return prefixes;
        }

        public bool SupportsPath(string virtualPath)
        {
            return prefixes.Any(s => virtualPath.StartsWith(s, StringComparison.Ordinal));
        }

        public async Task<IBlobData> Fetch(string virtualPath)
        {
            var prefix =  prefixes.FirstOrDefault(s => virtualPath.StartsWith(s, StringComparison.Ordinal));
            if (prefix == null)
            {
                return null;
            }

            var mapping = mappings[prefix];
            
            var key = string.IsNullOrEmpty(mapping.BlobPrefix)
                ? virtualPath.Substring(prefix.Length).TrimStart('/')
                : mapping.BlobPrefix + "/" + virtualPath.Substring(prefix.Length).TrimStart('/');

            try {
                var req = new Amazon.S3.Model.GetObjectRequest() { BucketName = mapping.Bucket, Key = key };

                var s = await client.GetObjectAsync(req);
                return new S3Blob(s);

            } catch (AmazonS3Exception se) {
                if (se.StatusCode == System.Net.HttpStatusCode.NotFound || "NoSuchKey".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                    throw new BlobMissingException($"Amazon S3 blob \"{key}\" not found.", se);
                if ( se.StatusCode == System.Net.HttpStatusCode.Forbidden || "AccessDenied".Equals(se.ErrorCode, StringComparison.OrdinalIgnoreCase)) 
                    throw new BlobMissingException($"Amazon S3 blob \"{key}\" not accessible. The blob may not exist or you may not have permission to access it.", se);
                throw;
            }
        }
    }
}