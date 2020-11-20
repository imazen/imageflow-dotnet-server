using System.IO;
using Imazen.Common.Extensibility.ClassicDiskCache;

namespace Imazen.Common.Extensibility.StreamCache
{
    public interface IStreamCacheResult
    {
        /// <summary>
        /// An open stream to the data. Must be disposed by caller.
        /// </summary>
        Stream Data { get; set; }

        /// <summary>
        /// null, or the content type if one was requested. 
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// The result of the cache check
        /// </summary>
        StreamCacheQueryResult Result { get; set; }
    }
}