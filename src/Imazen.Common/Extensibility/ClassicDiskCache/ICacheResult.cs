using System.IO;

namespace Imazen.Common.Extensibility.ClassicDiskCache
{
    public interface ICacheResult
    {
        /// <summary>
        /// The physical path to the cached item. Verify .Data is null before trying to read from this file.
        /// </summary>
        string PhysicalPath { get; }

        /// <summary>
        /// Provides a read-only stream to the data. Usually a MemoryStream instance, but you should dispose it once you are done. 
        /// If this value is not null, it indicates that the file has not yet been written to disk, and you should read it from this stream instead.
        /// </summary>
        Stream Data { get; set; }

        /// <summary>
        /// The path relative to the cache
        /// </summary>
        string RelativePath { get; }

        /// <summary>
        /// The result of the cache check
        /// </summary>
        CacheQueryResult Result { get; set; }
    }

}