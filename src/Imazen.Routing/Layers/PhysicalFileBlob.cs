using Imazen.Abstractions.Blobs;

namespace Imazen.Routing.Layers
{
    internal static class PhysicalFileBlobHelper
    {
        
        internal static StreamBlob CreateConsumableBlob(string path, DateTime lastModifiedDateUtc)
        {
            return new StreamBlob(new BlobAttributes()
            {
                LastModifiedDateUtc = lastModifiedDateUtc,
            }, new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan), null);
        }
    }
}