using Imazen.Abstractions.Blobs;

namespace Imazen.HybridCache.MetaStore
{
    internal enum LogEntryType: byte
    {
        Create = 0,
        Update = 1,
        Delete = 2
    }
    internal struct LogEntry(LogEntryType entryType, ICacheDatabaseRecord record)
    {
        internal LogEntryType EntryType = entryType;
        internal int AccessCountKey = record.AccessCountKey;
        internal DateTimeOffset CreatedAt = record.CreatedAt;
        internal DateTimeOffset LastDeletionAttempt = record.LastDeletionAttempt;
        internal long EstBlobDiskSize = record.EstDiskSize;
        internal string RelativePath = record.RelativePath;
        internal string? ContentType = record.ContentType;
        internal CacheEntryFlags Category = record.Flags;

        internal IReadOnlyList<SearchableBlobTag>? Tags = record.Tags;


        public CacheDatabaseRecord ToRecord()
        {
            return new CacheDatabaseRecord()
            {
                AccessCountKey = AccessCountKey,
                ContentType = ContentType,
                CreatedAt = CreatedAt,
                EstDiskSize = EstBlobDiskSize,
                LastDeletionAttempt = LastDeletionAttempt,
                RelativePath = RelativePath,
                Flags = Category,
                Tags = Tags
            };
        }
    }
}