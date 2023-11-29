using Imazen.Abstractions.Blobs;
using Imazen.Common.Extensibility.Support;

namespace Imazen.HybridCache
{
    internal interface ICacheDatabaseRecord : ICacheDatabaseRecordReference, IEstimateAllocatedBytesRecursive
    {
        int AccessCountKey { get; }
        DateTimeOffset CreatedAt { get; }
        DateTimeOffset LastDeletionAttempt { get;  }
        /// <summary>
        /// Estimated size on disk of the blob contents, excluding all metadata
        /// </summary>
        long EstDiskSize { get; }
        /// May not exist on disk
        string RelativePath { get; }
        string? ContentType { get; }
        CacheEntryFlags Flags { get;  }
        
        IReadOnlyList<SearchableBlobTag>? Tags { get; }
        
    }
}