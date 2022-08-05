using System.IO;
using Imazen.Common.Extensibility.ClassicDiskCache;

namespace Imazen.Common.Extensibility.StreamCache
{
    public interface IStreamCacheResult
    {
        /// <summary>
        /// An open stream to the data. Must be disposed by caller. Null if something went so wrong that even an uncached result isn't available
        /// </summary>
        Stream Data { get; }

        /// <summary>
        /// null, or the content type if one was requested. 
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// The result of the cache check.
        /// </summary>
        string Status { get; }
    }
}