namespace Imazen.HybridCache
{
    internal readonly struct CacheEntry
    {
        public CacheEntry(byte[] keyBasis, HashBasedPathBuilder builder)
        {
            Hash = builder.HashKeyBasis(keyBasis);
            PhysicalPath = builder.GetPhysicalPathFromHash(Hash);
            DisplayPath = builder.GetDisplayPathFromHash(Hash);
            StringKey = builder.GetStringFromHash(Hash);
        }

        public byte[] Hash { get; }
        public string PhysicalPath { get; }
        public string DisplayPath { get; }
        public string StringKey { get; }
    }
}