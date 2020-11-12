using System.Threading.Tasks;
using Imazen.Common.Storage;

namespace Imageflow.Server
{
    internal class BlobFetchCache
    {
        public BlobFetchCache(string virtualPath, BlobProvider provider)
        {
            this.virtualPath = virtualPath;
            this.provider = provider;
            resultFetched = false;
            blobFetched = false;
        }

        private readonly BlobProvider provider;
        private readonly string virtualPath;
        private bool resultFetched;
        private bool blobFetched;
        private BlobProviderResult? result;
        private IBlobData blob;

        internal BlobProviderResult? GetBlobResult()
        {
            if (resultFetched) return result;
            result = provider.GetResult(virtualPath);
            resultFetched = true;
            return result;
        }

        internal async Task<IBlobData> GetBlob()
        {
            if (blobFetched) return blob;
            var blobResult = GetBlobResult();
            if (blobResult != null) blob = await blobResult.Value.GetBlob();
            blobFetched = true;
            return blob;
        }

    }
}