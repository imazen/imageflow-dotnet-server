using Amazon;
using Amazon.S3;

namespace Imageflow.Server.Storage.S3
{
    internal struct PrefixMapping
    {
        internal string Prefix;
        internal AmazonS3Config Config;
        internal string Bucket;
        internal string BlobPrefix;
        internal bool IgnorePrefixCase;
        internal bool LowercaseBlobPath; 
    }
}