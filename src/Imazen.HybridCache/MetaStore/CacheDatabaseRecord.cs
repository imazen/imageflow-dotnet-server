using Imazen.Abstractions.Blobs;
using Imazen.Common.Extensibility.Support;

namespace Imazen.HybridCache.MetaStore
{

    internal class CacheDatabaseRecord: ICacheDatabaseRecord   
    {
        public CacheDatabaseRecord()
        {
            
        }
     
        public required int AccessCountKey { get; set; }
        public required DateTimeOffset CreatedAt { get; set; }
        public required DateTimeOffset LastDeletionAttempt { get; set; }
        public required long EstDiskSize { get; set; }
        public required string RelativePath { get; set; }
        public required string? ContentType { get; set; }
        public required CacheEntryFlags Flags { get; set; }
        public required IReadOnlyList<SearchableBlobTag>? Tags { get; set;  }
        public string CacheReferenceKey => RelativePath;
  
        
        public int EstimateAllocatedBytesRecursive => 
            24 + 
            AccessCountKey.EstimateMemorySize(false) +
            CreatedAt.EstimateMemorySize(false) +
            LastDeletionAttempt.EstimateMemorySize(false) +
            EstDiskSize.EstimateMemorySize(false) +
            RelativePath.EstimateMemorySize(true) +
            ContentType.EstimateMemorySize(true) +
            Flags.EstimateMemorySize(false) +
            Tags.EstimateMemorySize(true);

        internal int EstimateSerializedRowByteCount()
        {
            // Presuming no utf8 chars
            var count = 0;
            count += 4; //AccessCountKey
            count += 8; //CreatedAt
            count += 8; //LastDeletionAttempt
            count += 8; //DiskSize
            count += 1; //Flags
            
            count += 4; //RelativePath length 
            count += RelativePath.Length; //RelativePath
            count += 4; //ContentType length
            count += ContentType?.Length ?? 0; //ContentType
            count += 1; //Tags length 
            if (Tags != null)
            {
                foreach (var tag in Tags)
                {
                    count += 8; //key length, value length
                    count += tag.Key.Length; // key
                    count += tag.Value.Length; //value
                }
            }

            return count;
        }


    }

}