using Imazen.Abstractions.Blobs;
using Imazen.Common.Extensibility.Support;

namespace Imageflow.Server.Storage.S3;

internal record S3BlobStorageReference(string BucketName, string Key) : IBlobStorageReference
{
    public string GetFullyQualifiedRepresentation()
    {
        return $"s3://{BucketName}/{Key}";
    }

    public int EstimateAllocatedBytesRecursive => 24 + BucketName.EstimateMemorySize(true) + Key.EstimateMemorySize(true);
}