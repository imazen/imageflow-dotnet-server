namespace Imageflow.Server.HybridCache;

public class HybridCacheOptions : Imazen.HybridCache.HybridCacheOptions
{
    public HybridCacheOptions(string cacheDir) : base(cacheDir)
    {
    }

    public HybridCacheOptions(string uniqueName, string cacheDir) : base(uniqueName, cacheDir)
    {
    }
}