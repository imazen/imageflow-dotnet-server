namespace Imazen.HybridCache.MetaStore
{
    public class MetaStoreOptions
    {
        public MetaStoreOptions(string databaseDir)
        {
            DatabaseDir = databaseDir;
        }
        /// <summary>
        /// Where to store the database files
        /// </summary>
        public string DatabaseDir { get; set; }
        /// <summary>
        /// Creates 2^ShardCountBits shards. 3 is a good default
        /// </summary>
        public int ShardCountBits { get; set; } = 0;

        /// <summary>
        /// The maximum number of log files per shard to permit. Log files will be merged when this value is exceeded.
        /// </summary>
        public int MaxLogFilesPerShard { get; set; } = 5;
    }
}