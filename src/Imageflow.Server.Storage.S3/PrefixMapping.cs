using Amazon;

namespace Imageflow.Server.Storage.S3
{
    internal struct PrefixMapping
    {
        internal string Prefix;
        internal RegionEndpoint Region;
        internal string Bucket;
        internal string BlobPrefix;
        internal bool IgnorePrefixCase;
        internal bool LowercaseBlobPath; 
    }
}