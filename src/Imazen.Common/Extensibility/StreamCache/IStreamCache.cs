using Imazen.Common.Issues;
using Microsoft.Extensions.Hosting;

namespace Imazen.Common.Extensibility.StreamCache
{
    [Obsolete("Switch to IBlobCache; a callback system is no longer used")]
    public delegate Task<IStreamCacheInput> AsyncBytesResult(CancellationToken cancellationToken);
    
    [Obsolete("Implement IBlobCacheProvider and IBlobCache instead")]
    public interface IStreamCache : IIssueProvider, IHostedService
    {
        /// <summary>
        /// Requests a Stream to access the cached data for the given key. You must provide a callback to create the data if it doesn't exist.
        /// You can also request that the content type be retrieved as well, but in HybridCache this can cause a significant delay if done right after application start, since the write log will have to be loaded and resolved
        /// before that data is available.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="dataProviderCallback"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="retrieveContentType"></param>
        /// <returns></returns>
        Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult dataProviderCallback, CancellationToken cancellationToken, bool retrieveContentType);
    }
}