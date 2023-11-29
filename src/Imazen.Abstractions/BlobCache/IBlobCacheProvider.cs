namespace Imazen.Abstractions.BlobCache
{

    public interface IBlobCacheProvider
    {
        IEnumerable<IBlobCache> GetBlobCaches();
    }
}
