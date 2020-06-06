namespace Imazen.Common.Extensibility.ClassicDiskCache
{
    public enum CacheQueryResult
    {
        /// <summary>
        /// Failed to acquire a lock on the cached item within the timeout period
        /// </summary>
        Failed,

        /// <summary>
        /// The item wasn't cached, but was successfully added to the cache (or queued, in which case you should read .Data instead of .PhysicalPath)
        /// </summary>
        Miss,

        /// <summary>
        /// The item was already in the cache.
        /// </summary>
        Hit
    }

}