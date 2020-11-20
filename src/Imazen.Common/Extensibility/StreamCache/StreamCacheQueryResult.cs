namespace Imazen.Common.Extensibility.StreamCache
{
    public enum StreamCacheQueryResult
    {
        /// <summary>
        /// Failed to acquire a lock on the cached item within the timeout period
        /// </summary>
        Failed,

        /// <summary>
        /// The item wasn't cached, but was successfully added to the cache
        /// </summary>
        Miss,

        /// <summary>
        /// The item was already in the cache.
        /// </summary>
        Hit,
    }
}