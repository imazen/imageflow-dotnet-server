namespace Imageflow.Server.Storage.AzureBlob
{
    internal struct PrefixMapping
    {
        internal string UrlPrefix;
        internal string Container;
        internal string BlobPrefix;
        internal bool IgnorePrefixCase;
        internal bool LowercaseBlobPath; 
    }
}