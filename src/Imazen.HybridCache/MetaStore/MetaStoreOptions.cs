namespace Imazen.HybridCache.MetaStore
{
    public class MetaStoreOptions
    {
        public string DatabaseDir { get; set; }
        /// <summary>
        /// Creates 2^ShardCountBits shards
        /// </summary>
        public int ShardCountBits { get; set; } = 3;
    }
}