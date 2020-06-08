namespace Imageflow.Server.DiskCache
{
    public class DiskCacheSettings: Imazen.DiskCache.ClassicDiskCacheSettings
    {
        public DiskCacheSettings(string physicalCacheDir):base(physicalCacheDir){}
    }
}