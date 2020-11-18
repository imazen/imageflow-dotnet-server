// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the GNU Affero General Public License, Version 3.0.
// Commercial licenses available at http://imageresizing.net/
using System;
using System.Collections.Generic;

namespace Imazen.DiskCache.SourceMemCache {
    internal class ConstrainedCache<TK,TV> {

        public delegate long SizeCalculationDelegate(TK key, TV value);
        public ConstrainedCache(IEqualityComparer<TK> keyComparer, SizeCalculationDelegate calculator, long maxBytes, TimeSpan usageWindow, TimeSpan minCleanupInterval) {
            EventCountingStrategy s = new EventCountingStrategy
            {
                MaxBytesUsed = maxBytes, 
                MinimumCleanupInterval = minCleanupInterval, 
                CounterGranularity = 16
            };
            usage = new EventCountingDictionary<TK>(keyComparer, usageWindow, s);
            usage.CounterRemoved += usage_CounterRemoved;

            data = new Dictionary<TK, TV>(keyComparer);

            this.calculator = calculator;
        }

        private readonly SizeCalculationDelegate calculator;
        private readonly EventCountingDictionary<TK> usage;

        private readonly Dictionary<TK,TV> data;

        /// <summary>
        /// The estimated ram usage for the entire cache. Relies upon the accuracy of the calculator delegate
        /// </summary>
        public long ReportedBytesUsed => usage.ReportedBytesUsed;

        private readonly object lockSync = new object();

        public TV Get(TK key) {
            lock (lockSync) {
                bool found = data.TryGetValue(key, out var val);
                if (found) usage.Increment(key, 0);
                return found ? val : default;
            }
        }

        public void Set(TK key, TV val) {
            lock (lockSync) {
                data[key] = val;
                usage.Increment(key, calculator(key, val) + 32);
            }
        }

        public void PingCleanup() { usage.PingCleanup(); }

        void usage_CounterRemoved(EventCountingDictionary<TK> sender, TK key, int value) {
            //May be expected inside usage.lockSync AND lockSync
            lock (lockSync) {
                data.Remove(key);
            }
        }

    }
}
