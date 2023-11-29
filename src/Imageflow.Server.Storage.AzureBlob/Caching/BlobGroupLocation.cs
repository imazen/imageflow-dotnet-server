namespace Imageflow.Server.Storage.AzureBlob.Caching
{
    /// <summary>
    /// An immutable data structure that holds two strings,  Bucket, and BlobPrefix. 
    /// BlobPrefix is the prefix of the blob key, not including the bucket name. It cannot start with a slash, and must be a valid S3 blob string
    /// Bucket must be a valid S3 bucket name. Use BlobStringValidator
    /// </summary>
    internal readonly struct BlobGroupLocation
    {
        internal readonly string ContainerName;
        internal readonly string BlobPrefix;
        internal readonly BlobClientOrName AzureClient;

        // TOOD: create from DI using azure name

        internal BlobGroupLocation(string containerName, string blobPrefix, BlobClientOrName client)
        {
            //TODO
            // if (!BlobStringValidator.ValidateBucketName(containerName, out var error))
            // {
            //     throw new ArgumentException(error, nameof(containerName));
            // }
            // if (!BlobStringValidator.ValidateKeyPrefix(blobPrefix, out error))
            // {
            //     throw new ArgumentException(error, nameof(blobPrefix));
            // }
            ContainerName = containerName;
            BlobPrefix = blobPrefix;
            AzureClient = client;
        }

    }
}