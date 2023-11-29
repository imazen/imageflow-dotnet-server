using Imazen.Common.Extensibility.Support;

namespace Imazen.Abstractions.BlobCache;

public interface IBlobCacheRequest 
{
    /// <summary>
    /// Caches may use different storage backends for different categories of data, such as essential data, generated data, or metadata.
    /// Two requests for the same cache key may return different results if they are in different categories.
    /// </summary>
    BlobGroup BlobCategory { get; }
    
    /// <summary>
    /// 32 byte hash of the cache key
    /// </summary>
    byte[] CacheKeyHash { get; }
    
    /// <summary>
    /// An encoded string of the cache key hash
    /// </summary>
    string CacheKeyHashString { get; }
    
    /// <summary>
    /// May trigger additional disk or network requests to acquire metadata about the blob, with additional
    /// probability of failure. If false, even content-type may not be returned.
    /// </summary>
    bool FetchAllMetadata { get; }
    
    /// <summary>
    /// If true, caches should fail quickly if there is any kind of problem (such as a locked file or network error),
    /// instead of performing retries or waiting for resource availability.
    /// </summary>
    bool FailFast { get; }
    
    IBlobCacheRequestConditions? Conditions { get; }
}


public class BlobCacheRequest: IBlobCacheRequest
{
    public BlobCacheRequest(BlobGroup blobCategory, byte[] cacheKeyHash, string cacheKeyHashString, bool fetchAllMetadata)
    {
        BlobCategory = blobCategory;
        CacheKeyHash = cacheKeyHash;
        CacheKeyHashString = cacheKeyHashString;
        FetchAllMetadata = fetchAllMetadata;
    }
    
    public BlobCacheRequest(BlobGroup blobCategory, byte[] hashBasis)
    {
        BlobCategory = blobCategory;
        CacheKeyHash = HashBasedPathBuilder.HashKeyBasisStatic(hashBasis);
        CacheKeyHashString = HashBasedPathBuilder.GetStringFromHashStatic(CacheKeyHash);
        FetchAllMetadata = false;
        
    }
    
    public BlobCacheRequest(IBlobCacheRequest request)
    {
        BlobCategory = request.BlobCategory;
        CacheKeyHash = request.CacheKeyHash;
        CacheKeyHashString = request.CacheKeyHashString;
        FetchAllMetadata = request.FetchAllMetadata;
        FailFast = request.FailFast;
        Conditions = request.Conditions;
    }
    public BlobGroup BlobCategory { get; init; }
    public byte[] CacheKeyHash { get; init; }
    public string CacheKeyHashString { get; init; }
    
    
    /// <summary>
    /// Clarify this - many kinds of metadata
    /// </summary>
    public bool FetchAllMetadata { get; init; } = false;

    public bool FailFast { get; init; } = false;
    public IBlobCacheRequestConditions? Conditions { get; init; } = null;

    
}

public static class BlobCacheRequestExtensions
{
    public static IBlobCacheRequest WithFailFast(this IBlobCacheRequest request, bool failFast)
    {
        return new BlobCacheRequest(request) {FailFast = failFast};
    }
    
    public static IBlobCacheRequest WithFetchAllMetadata(this IBlobCacheRequest request, bool fetchAllMetadata)
    {
        return new BlobCacheRequest(request) {FetchAllMetadata = fetchAllMetadata};
    }
}