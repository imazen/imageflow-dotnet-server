/* Copyright (c) 2014 Imazen See license.txt for your rights. */

using System.Collections.Generic;
using Imazen.Common.Collections;

namespace Imazen.DiskCache.Cleanup {
    internal class CleanupQueue {
        readonly LinkedList<CleanupWorkItem> queue;
        public CleanupQueue() {
            queue = new LinkedList<CleanupWorkItem>();
        }

        private readonly object sync = new object();

        public void Queue(CleanupWorkItem item) {
            lock (sync) {
                queue.AddLast(item);
            }
        }

        /// <summary>
        /// Queues the item if no other identical items exist in the queue. Returns true if the item was added.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool QueueIfUnique(CleanupWorkItem item) {
            lock (sync) {
                bool unique = !queue.Contains(item);
                if (unique) queue.AddLast(item);
                return unique;
            }
        }

        public bool Exists(CleanupWorkItem item) {
            lock (sync) {
                return queue.Contains(item);
            }
        }
        public void Insert(CleanupWorkItem item) {
            lock (sync) {
                queue.AddFirst(item);
            }
        }
        public void QueueRange(IEnumerable<CleanupWorkItem> items) {
            lock (sync) {
                foreach (CleanupWorkItem item in items)
                {
                    queue.AddLast(item);
                }
            }
        }
        /// <summary>
        /// Inserts the specified list of items and the end of the queue. They will be next items popped.
        /// They will pop off the list in the same order they exist in 'items' (i.e, they are inserted in reverse order).
        /// </summary>
        /// <param name="items"></param>
        public void InsertRange(IList<CleanupWorkItem> items) {
            lock (sync) {
                ReverseEnumerable<CleanupWorkItem> reversed = new ReverseEnumerable<CleanupWorkItem>(new System.Collections.ObjectModel.ReadOnlyCollection<CleanupWorkItem>(items));
                foreach (CleanupWorkItem item in reversed)
                {
                    queue.AddFirst(item);

                }
            }
        }
        public CleanupWorkItem Pop() {
            lock (sync) {
                CleanupWorkItem i = queue.Count > 0 ? queue.First.Value : null;
                if (i != null) queue.RemoveFirst();
                return i;
            }
        }

        public bool IsEmpty {
            get {
                lock (sync) return queue.Count <= 0;
            }
        }
        public int Count {
            get {
                lock (sync) return queue.Count;
            }
        }
        public void Clear() {
            lock (sync) {
                queue.Clear();
            }
        }
        /// <summary>
        /// Performs an atomic clear and enqueue of the specified item
        /// </summary>
        /// <param name="item"></param>
        public void ReplaceWith(CleanupWorkItem item) {
            lock (sync) {
                queue.Clear();
                queue.AddLast(item);
            }
        }
    }
}
