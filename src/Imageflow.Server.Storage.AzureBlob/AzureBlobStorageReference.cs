using Imazen.Abstractions.Blobs;
using Imazen.Common.Extensibility.Support;

namespace Imageflow.Server.Storage.AzureBlob;

internal record AzureBlobStorageReference(string AccountAndContainerPrefix, string BlobName) : IBlobStorageReference
{
    public string GetFullyQualifiedRepresentation()
    {
        return $"{AccountAndContainerPrefix}/{BlobName}";
    }

    public int EstimateAllocatedBytesRecursive => 
        24 + AccountAndContainerPrefix.EstimateMemorySize(true) + BlobName.EstimateMemorySize(true);
}