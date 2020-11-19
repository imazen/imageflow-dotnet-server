namespace Imazen.HybridCache
{
    internal readonly struct CacheEntry
    {
        public CacheEntry(string physicalPath, string relativePath)
        {
            PhysicalPath = physicalPath;
            RelativePath = relativePath;
            //Lock execution using relativePath as the sync basis. Ignore casing differences.
            LockingKey = relativePath.ToUpperInvariant();
        }

        public string PhysicalPath { get; }
        public string RelativePath { get; }
        public string LockingKey { get; }
    }
}