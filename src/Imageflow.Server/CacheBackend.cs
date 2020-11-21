namespace Imageflow.Server
{
    internal enum CacheBackend
    {
        ClassicDiskCache,
        SqliteCache,
        MemoryCache,
        DistributedCache,
        StreamCache,
        NoCache,
    }
}