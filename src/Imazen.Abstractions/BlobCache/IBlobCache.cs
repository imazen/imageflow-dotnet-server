global using CacheFetchResult = Imazen.Abstractions.Resulting.IResult<Imazen.Abstractions.Blobs.IBlobWrapper, Imazen.Abstractions.BlobCache.IBlobCacheFetchFailure>;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;

namespace Imazen.Abstractions.BlobCache
{
    public interface IBlobCache: IUniqueNamed
    {

        
        /// <summary>
        /// The cache should attempt to fetch the blob, and return a result indicating whether it was found or not.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<CacheFetchResult> CacheFetch(IBlobCacheRequest request, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// The cache should attempt to store the blob, if the conditions are met.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<CodeResult> CachePut(ICacheEventDetails e, CancellationToken cancellationToken = default);

        
        /// <summary>
        /// Return references to all entries with the given tag
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<CodeResult<IAsyncEnumerable<IBlobStorageReference>>> CacheSearchByTag(SearchableBlobTag tag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempt to delete all entries with the given tag, and let us know the result for each attempt.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<CodeResult<IAsyncEnumerable<CodeResult<IBlobStorageReference>>>> CachePurgeByTag(SearchableBlobTag tag, CancellationToken cancellationToken = default);

        
        /// <summary>
        /// Try to delete a given cache entry. 
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<CodeResult> CacheDelete(IBlobStorageReference reference, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called for many different events, including external cache hits, misses, and errors.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<CodeResult> OnCacheEvent(ICacheEventDetails e, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Returns the expected cache capabilities expected with the current settings, assuming no errors.
        /// </summary>
        BlobCacheCapabilities InitialCacheCapabilities { get; }
        
        /// <summary>
        /// Performs a full health check of the cache, including any external dependencies.
        /// Returns errors and the current capability level.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<IBlobCacheHealthDetails> CacheHealthCheck(CancellationToken cancellationToken = default);
    }
}