/* Copyright (c) 2014 Imazen See license.txt for your rights. */

using System;
using System.IO;

namespace Imazen.DiskCache.Index {

    internal class CachedFileInfo {
        public CachedFileInfo(FileSystemInfo f) {
            AccessedUtc = f.LastAccessTimeUtc;
            UpdatedUtc = f.CreationTimeUtc;
        }
        /// <summary>
        /// Uses old.AccessedUtc if it is newer than FileInfo.LastAccessTimeUtc
        /// </summary>
        /// <param name="f"></param>
        /// <param name="old"></param>
        public CachedFileInfo(FileSystemInfo f, CachedFileInfo old) {
            AccessedUtc = f.LastAccessTimeUtc;
            if (old != null && AccessedUtc < old.AccessedUtc) AccessedUtc = old.AccessedUtc; //Use the larger value
            UpdatedUtc = f.CreationTimeUtc;
        }
        
        public CachedFileInfo(DateTime createdDate, DateTime accessedDate) {
            UpdatedUtc = createdDate;
            AccessedUtc = accessedDate;
        }
        public CachedFileInfo(CachedFileInfo f, DateTime accessedDate) {
            UpdatedUtc = f.UpdatedUtc;
            AccessedUtc = accessedDate;
        }
        

        /// <summary>
        /// The last time the file was accessed. Will not match NTFS date, this value is updated by DiskCache.
        /// When first loaded from NTFS, it will be granular to about an hour, due to NTFS delayed write. Also, windows Vista and higher never write accessed dates. 
        /// We update this value in memory, and flush it to disk lazily. 
        /// </summary>
        public DateTime AccessedUtc { get; }

        /// <summary>
        /// The Created date of the cached file - the last time the cached file was written to
        /// </summary>
        public DateTime UpdatedUtc { get; }
    }
}
