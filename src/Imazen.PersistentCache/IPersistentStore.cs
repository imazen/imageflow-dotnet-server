using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.PersistentCache
{
    public interface IPersistentStore
    {
        /// <summary>
        /// Returns null if blob does not exist
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<Stream> ReadStream(uint shard, string key, CancellationToken cancellationToken);

        /// <summary>
        /// Lists all keys in the given shard that are within the given parent key, as deonted by forward-slashes. 
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="parentKey"></param>
        /// <returns></returns>
        Task<IEnumerable<IBlobInfo>> List(uint shard, string parentKey, CancellationToken cancellationToken);

        /// <summary>
        /// Write and flush the given data to the specified key
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task WriteBytes(uint shard, string key, byte[] data, CancellationToken cancellationToken);

        /// <summary>
        ///  Delete the given key. Succeeds silently if key does not exist. 
        /// </summary>
        /// <param name="shard"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        Task Delete(uint shard, string key, CancellationToken cancellationToken);

    }
}
