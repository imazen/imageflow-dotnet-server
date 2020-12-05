// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the GNU Affero General Public License, Version 3.0.
// Commercial licenses available at http://imageresizing.net/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Imazen.HybridCache {
    internal class AsyncWriteCollection {

        public AsyncWriteCollection(long maxQueueBytes) {
            MaxQueueBytes = maxQueueBytes;
        }

        private readonly object sync = new object();

        private readonly Dictionary<string, AsyncWrite> c = new Dictionary<string, AsyncWrite>();

        /// <summary>
        /// How many bytes of buffered file data to hold in memory before refusing further queue requests and forcing them to be executed synchronously.
        /// </summary>
        public long MaxQueueBytes { get; }

        private long queuedBytes;
        /// <summary>
        /// If the collection contains the specified item, it is returned. Otherwise, null is returned.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public AsyncWrite Get(string key) {
            lock (sync) {
                return c.TryGetValue(key, out var result) ? result : null;
            }
        }
        
        /// <summary>
        /// Removes the specified object based on its relativePath and modifiedDateUtc values.
        /// </summary>
        /// <param name="w"></param>
        private void Remove(AsyncWrite w) {
            lock (sync)
            {
                c.Remove(w.Key);
                queuedBytes -= w.GetEntrySizeInMemory();
            }
        }

        public enum AsyncQueueResult
        {
            Enqueued,
            AlreadyPresent,
            QueueFull
        }
        /// <summary>
        /// Tries to enqueue the given async write and callback
        /// </summary>
        /// <param name="w"></param>
        /// <param name="writerDelegate"></param>
        /// <returns></returns>
        public AsyncQueueResult Queue(AsyncWrite w, Func<AsyncWrite, Task> writerDelegate){
            lock (sync)
            {
                if (queuedBytes < 0) throw new InvalidOperationException();
                if (queuedBytes + w.GetEntrySizeInMemory() > MaxQueueBytes) return AsyncQueueResult.QueueFull; //Because we would use too much ram.
                if (c.ContainsKey(w.Key)) return AsyncQueueResult.AlreadyPresent; //We already have a queued write for this data.
                c.Add(w.Key, w);
                queuedBytes += w.GetEntrySizeInMemory();
                w.RunningTask = Task.Run(
                    async () => {
                        try
                        {
                            await writerDelegate(w);
                        }
                        finally
                        {
                            Remove(w);
                        }
                    });
                return AsyncQueueResult.Enqueued;
            }
        }

        public Task AwaitAllCurrentTasks()
        {
            lock (sync)
            {
                var tasks = c.Values.Select(w => w.RunningTask).ToArray();
                return Task.WhenAll(tasks);
            }
        }
    }
}
