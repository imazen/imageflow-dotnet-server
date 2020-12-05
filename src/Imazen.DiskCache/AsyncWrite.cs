// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the GNU Affero General Public License, Version 3.0.
// Commercial licenses available at http://imageresizing.net/
using System;
using System.IO;

namespace Imazen.DiskCache {
    internal class AsyncWrite {

        public AsyncWrite(MemoryStream data, string physicalPath, string key) {
            this.data = data;
            PhysicalPath = physicalPath;
            Key = key;
            JobCreatedAt = DateTime.UtcNow;
        }
        
        public string PhysicalPath { get; }

        public string Key { get; }
        /// <summary>
        /// Returns the UTC time this AsyncWrite object was created.
        /// </summary>
        public DateTime JobCreatedAt { get; }
        
        private readonly MemoryStream data;
        
        /// <summary>
        /// Returns the length of the buffer capacity
        /// </summary>
        /// <returns></returns>
        public long GetBufferLength() {
            return data.Capacity;
        }

        /// <summary>
        /// Wraps the data in a readonly MemoryStream so it can be accessed on another thread
        /// </summary>
        /// <returns></returns>
        public MemoryStream GetReadonlyStream() {
            //Wrap the original buffer in a new MemoryStream.
            return new MemoryStream(data.GetBuffer(), 0, (int)data.Length, false, true);
        }
    }
}
