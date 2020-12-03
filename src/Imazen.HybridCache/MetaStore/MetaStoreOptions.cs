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
        /// The number of shards to create
        /// </summary>
        public int Shards { get; set; } = 1;

        /// <summary>
        /// The maximum number of log files per shard to permit. Log files will be merged when this value is exceeded.
        /// </summary>
        public int MaxLogFilesPerShard { get; set; } = 5;
    }
}