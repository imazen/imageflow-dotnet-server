using System;
using System.Collections.Generic;

namespace Imazen.HybridCache
{
    internal interface ICacheDatabaseRecord
    {
        long CreatedAt { get; }
        long LastDeletionAttempt { get;  }
        long DiskSize { get; }
        string RelativePath { get; }
    }
    internal interface ICacheDatabase
    {
        void UpdateLastDeletionAttempt(string relativePath, DateTimeOffset when);
        
        void CreateRecord(string relativePath, DateTimeOffset createdAt, long diskSize);
        bool RecordExists(string relativePath);
        void DeleteRecord(string relativePath);

        IEnumerable<ICacheDatabaseRecord> GetOldestRecords(long maxLastDeletionAttemptTime, long diskBytesToReturn);
        long GetTotalBytes();
        
        
        
    }
}