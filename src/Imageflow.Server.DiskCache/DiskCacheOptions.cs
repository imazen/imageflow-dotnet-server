namespace Imageflow.Server.DiskCache
{
    public class DiskCacheOptions: Imazen.DiskCache.ClassicDiskCacheOptions
    {
        public DiskCacheOptions(string physicalCacheDir):base(physicalCacheDir){}
    }
}