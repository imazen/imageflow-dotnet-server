/* Copyright (c) 2014 Imazen See license.txt for your rights. */
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Imazen.DiskCache {

    internal class CachedFileInfo {
        public CachedFileInfo(FileInfo f) {
            ModifiedUtc = f.LastWriteTimeUtc;
            AccessedUtc = f.LastAccessTimeUtc;
            UpdatedUtc = f.CreationTimeUtc;
        }
        /// <summary>
        /// Uses old.AccessedUtc if it is newer than FileInfo.LastAccessTimeUtc
        /// </summary>
        /// <param name="f"></param>
        /// <param name="old"></param>
        public CachedFileInfo(FileInfo f, CachedFileInfo old) {
            ModifiedUtc = f.LastWriteTimeUtc;
            AccessedUtc = f.LastAccessTimeUtc;
            if (old != null && AccessedUtc < old.AccessedUtc) AccessedUtc = old.AccessedUtc; //Use the larger value
            UpdatedUtc = f.CreationTimeUtc;
        }

        public CachedFileInfo(DateTime modifiedDate, DateTime createdDate) {
            this.ModifiedUtc = modifiedDate;
            this.UpdatedUtc = createdDate;
            this.AccessedUtc = createdDate;
        }
        public CachedFileInfo(DateTime modifiedDate, DateTime createdDate, DateTime accessedDate) {
            this.ModifiedUtc = modifiedDate;
            this.UpdatedUtc = createdDate;
            this.AccessedUtc = accessedDate;
        }
        public CachedFileInfo(CachedFileInfo f, DateTime accessedDate) {
            this.ModifiedUtc = f.ModifiedUtc;
            this.UpdatedUtc = f.UpdatedUtc;
            this.AccessedUtc = accessedDate;
        }

        /// <summary>
        /// The modified date of the source file that the cached file is based on.
        /// </summary>
        public DateTime ModifiedUtc { get; }

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
