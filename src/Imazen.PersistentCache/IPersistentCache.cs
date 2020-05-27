using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.PersistentCache
{
    public interface IPersistentCache
    {
        // Summary:
        // Triggered when the application host is ready to start the service.
        Task StartAsync(CancellationToken cancellationToken);

        // Summary:
        // Triggered when the application host is performing a graceful shutdown.
        Task StopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Returns null if the key cannot be found. 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<Stream> GetStream(CacheKey key, CancellationToken cancellationToken);

        /// <summary>
        ///  Enqueues the cache write for later flushing to storage
        /// </summary>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <param name="cost"></param>
        void PutBytesEventually(CacheKey key, byte[] data, uint cost);

        /// <summary>
        /// Delete all cache entries that match on key1. Useful for purging sensitive information by source filename. 
        /// </summary>
        /// <param name="key1"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task EvictByKey1(byte[] key1, CancellationToken cancellationToken);

        /// <summary>
        /// Delete all cache entries that match on key1 but not on key2. 
        /// Useful for deleting older versions of a file. 
        /// </summary>
        /// <param name="includingKey1"></param>
        /// <param name="excludingKey2"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task EvictByKey1ExcludingKey2(byte[] includingKey1, byte[] excludingKey2, CancellationToken cancellationToken);

    }
}
