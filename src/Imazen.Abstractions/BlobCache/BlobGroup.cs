namespace Imazen.Abstractions.BlobCache
{

    /// <summary>
    /// Which container or sub-container to store or fetch a blob from. Different groups may have different eviction policies.
    /// </summary>
    public enum BlobGroup : byte{
        /// <summary>
        /// For the image data contents of a cache entry (may also contain associated metadata)
        /// </summary>
        GeneratedCacheEntry,
        
        /// <summary>
        /// For files that are simply proxied from another source, such as a remote URL or a file on disk.
        /// These may utilize a lower eviction policy than GeneratedCacheEntry.
        /// </summary>
        ProxiedCacheEntry,
        
        /// <summary>
        /// For small, non-image-data blobs that contain metadata or other information about a cache entry that should not be evicted as eagerly as the image data.
        /// </summary>
        CacheEntryMetadata,

        /// <summary>
        /// For small blobs that contain metadata or other information about a source image that should generally not be evicted. 
        /// </summary>
        SourceMetadata,
        
        /// <summary>
        /// For essential blobs that should never be evicted, such as index files and tag data.
        /// </summary>
        Essential,
    }
}