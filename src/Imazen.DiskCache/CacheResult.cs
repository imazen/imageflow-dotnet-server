/* Copyright (c) 2014 Imazen See license.txt for your rights. */

using System.IO;
using Imazen.Common.Extensibility.ClassicDiskCache;

namespace Imazen.DiskCache {
    public class CacheResult : ICacheResult
    {
        public CacheResult(CacheQueryResult result, string physicalPath, string relativePath) {
            Result = result;
            PhysicalPath = physicalPath;
            RelativePath = relativePath;
        }
        
        /// <summary>
        /// The physical path to the cached item. Verify .Data is null before trying to read from this file.
        /// </summary>
        public string PhysicalPath { get; }

        /// <summary>
        /// Provides a read-only stream to the data. Usually a MemoryStream instance, but you should dispose it once you are done. 
        /// If this value is not null, it indicates that the file has not yet been written to disk, and you should read it from this stream instead.
        /// </summary>
        public Stream Data { get; set; }


        /// <summary>
        /// The path relative to the cache
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// The result of the cache check
        /// </summary>
        public CacheQueryResult Result { get; set; }
    }
}
