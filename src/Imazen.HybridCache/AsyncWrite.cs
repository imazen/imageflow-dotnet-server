// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the GNU Affero General Public License, Version 3.0.
// Commercial licenses available at http://imageresizing.net/
using System;
using System.IO;
using System.Threading.Tasks;

namespace Imazen.HybridCache {
    internal class AsyncWrite {

        public AsyncWrite(string key, ArraySegment<byte> data,  string contentType) {
            this.data = data;
            if (data.Array == null) throw new ArgumentException(nameof(data));
            Key = key;
            ContentType = contentType;
            JobCreatedAt = DateTime.UtcNow;
        }
        private readonly ArraySegment<byte> data;
        
        public string ContentType { get; }

        public Task RunningTask { get; set; }
        public string Key { get; }
        /// <summary>
        /// Returns the UTC time this AsyncWrite object was created.
        /// </summary>
        public DateTime JobCreatedAt { get; }

        /// <summary>
        /// Returns the length of the buffer capacity
        /// </summary>
        /// <returns></returns>
        public long GetBufferLength() {
            return data.Array?.Length ?? 0;
        }
        /// <summary>
        /// Returns just the number of bytes used within the buffer
        /// </summary>
        /// <returns></returns>
        public int GetUsedBytes() {
            return data.Count;
        }

        /// <summary>
        /// Wraps the data in a readonly MemoryStream so it can be accessed on another thread
        /// </summary>
        /// <returns></returns>
        public MemoryStream GetReadonlyStream() {
            //Wrap the original buffer in a new MemoryStream.
            return new MemoryStream(data.Array ?? throw new NullReferenceException(), data.Offset, data.Count, false, true);
        }
    }
}
