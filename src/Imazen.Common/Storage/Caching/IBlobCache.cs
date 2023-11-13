using System.Threading;
using System.Threading.Tasks;

namespace Imazen.Common.Storage.Caching
{
    public interface IBlobCache
    {
        Task<ICacheBlobPutResult> Put(BlobGroup group, string key, IBlobData data, ICacheBlobPutOptions options, CancellationToken cancellationToken = default);

        Task<bool> MayExist(BlobGroup group, string key, CancellationToken cancellationToken = default);

        Task<ICacheBlobFetchResult> TryFetchBlob(BlobGroup group, string key, CancellationToken cancellationToken = default);

    }
}