using System;

namespace Imazen.HybridCache.Sqlite
{
    internal class CacheDatabaseRecord: ICacheDatabaseRecord   
    {
        public int AccessCountKey { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastDeletionAttempt { get; set; }
        public long DiskSize { get; set; }
        public string RelativePath { get; set; }
        public string ContentType { get; set; }
    }
}