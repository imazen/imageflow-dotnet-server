using System;
using Imazen.HybridCache.Sqlite;

namespace Imazen.HybridCache.MetaStore
{
    internal enum LogEntryType
    {
        Create = 0,
        Update = 1,
        Delete = 2
    }
    internal struct LogEntry
    {
        internal LogEntryType EntryType;
        internal int AccessCountKey;
        internal DateTime CreatedAt;
        internal DateTime LastDeletionAttempt;
        internal long DiskSize;
        internal string RelativePath;
        internal string ContentType;

        public LogEntry(LogEntryType entryType, ICacheDatabaseRecord record)
        {
            EntryType = entryType;
            AccessCountKey = record.AccessCountKey;
            ContentType = record.ContentType;
            RelativePath = record.RelativePath;
            DiskSize = record.DiskSize;
            LastDeletionAttempt = record.LastDeletionAttempt;
            CreatedAt = record.CreatedAt;
        }

        public CacheDatabaseRecord ToRecord()
        {
            return new CacheDatabaseRecord()
            {
                AccessCountKey = AccessCountKey,
                ContentType = ContentType,
                CreatedAt = CreatedAt,
                DiskSize = DiskSize,
                LastDeletionAttempt = LastDeletionAttempt,
                RelativePath = RelativePath
            };
        }
    }
}