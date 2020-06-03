/* Copyright (c) 2014 Imazen See license.txt for your rights. */
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Imazen.DiskCache {
    public enum CacheQueryResult {
        /// <summary>
        /// Failed to acquire a lock on the cached item within the timeout period
        /// </summary>
        Failed,
        /// <summary>
        /// The item wasn't cached, but was successfully added to the cache (or queued, in which case you should read .Data instead of .PhysicalPath)
        /// </summary>
        Miss,
        /// <summary>
        /// The item was already in the cache.
        /// </summary>
        Hit
    }
    public class CacheResult {
        public CacheResult(CacheQueryResult result, string physicalPath, string relativePath) {
            this.Result = result;
            this.PhysicalPath = physicalPath;
            this.RelativePath = relativePath;
        }
        public CacheResult(CacheQueryResult result, Stream data, string relativePath) {
            this.Result = result;
            this.Data = data;
            this.RelativePath = relativePath;
        }

        /// <summary>
        /// The physical path to the cached item. Verify .Data is null before trying to read from this file.
        /// </summary>
        public string PhysicalPath { get; } = null;

        /// <summary>
        /// Provides a read-only stream to the data. Usually a MemoryStream instance, but you should dispose it once you are done. 
        /// If this value is not null, it indicates that the file has not yet been written to disk, and you should read it from this stream instead.
        /// </summary>
        public Stream Data { get; set; } = null;


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
