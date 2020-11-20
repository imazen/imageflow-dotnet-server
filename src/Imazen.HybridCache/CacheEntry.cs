using System;

namespace Imazen.HybridCache
{
    internal readonly struct CacheEntry
    {
        public CacheEntry(byte[] keyBasis, HashBasedPathBuilder builder)
        {
            Hash = builder.HashKeyBasis(keyBasis);
            RelativePath = builder.GetRelativePathFromHash(Hash);
            PhysicalPath = builder.GetPhysicalPathFromRelativePath(RelativePath);
            StringKey = builder.GetStringFromHash(Hash);
            GetKeyBytesFromStringKey = builder.GetHashFromString;
        }

        public byte[] Hash { get; }
        public string PhysicalPath { get; }
        public string DisplayPath => RelativePath;
        public string StringKey { get; }
        
        public string RelativePath { get; }
        
        public Func<string, byte[]> GetKeyBytesFromStringKey { get; }
    }
}