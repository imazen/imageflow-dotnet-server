using System;
using System.Collections.Generic;
using System.Text;

namespace Imazen.PersistentCache
{
    public class PersistentCacheSettings
    {
        /// <summary>
        /// You must manually delete all cached data when reducing the shard count. 
        /// Determines how many shards to slice the read and write caches into
        /// </summary>
        public uint ShardCount { get; set; } = 1;

        /// <summary>
        /// How many minutes between halving the usage frequency counter. Defaults to 30.
        /// </summary>
        public uint UsageFrequencyHalfLifeMinutes { get; set; } = 30;

        /// <summary>
        /// The maximum number of bytes to cache across all shards
        /// </summary>
        public ulong MaxCachedBytes { get; set; } = 1024 * 1024 * 1024; 

        /// <summary>
        /// The percentage (between 0 and 100) of total space that should be reclaimed when evicting cache entries. Defaults to 10
        /// </summary>
        public float FreeSpacePercentGoal { get; set; } = 10;

        /// <summary>
        /// How often to flush the write log in milliseconds. Defaults to 30 seconds
        /// </summary>
        public int WriteLogFlushIntervalMs { get; set; } = 30 * 1000;

        /// <summary>
        /// The maximum size (in bytes) to make a write log. Defaults to 409kb.
        /// </summary>
        public ulong MaxWriteLogSize { get; set; } = 4096 * 100;

        /// <summary>
        /// How often to flush the read tree in milliseconds. Defaults to 5 minutes.
        /// </summary>
        public int ReadInfoFlushIntervalMs { get; set; } = 60 * 5 * 1000;
    }
}
