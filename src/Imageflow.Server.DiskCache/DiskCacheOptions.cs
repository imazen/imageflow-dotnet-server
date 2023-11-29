using Imazen.Abstractions;

namespace Imageflow.Server.DiskCache
{
    public class DiskCacheOptions: Imazen.DiskCache.ClassicDiskCacheOptions, IUniqueNamed
    {
        [System.Obsolete("Use DiskCacheOptions(string uniqueName, string physicalCacheDir) instead")]
        public DiskCacheOptions(string physicalCacheDir):base(physicalCacheDir){
            this.UniqueName = "LegacyDiskCache";
        }
        public DiskCacheOptions(string uniqueName, string physicalCacheDir):base(physicalCacheDir){
            this.UniqueName = uniqueName;
        }

        public string UniqueName { get; }

    }
}