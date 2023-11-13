namespace Imazen.Common.Storage.Caching
{

    /// <summary>
    /// Which container or sub-container to store or fetch a blob from. Different groups may have different eviction policies.
    /// </summary>
    public enum BlobGroup{
        /// <summary>
        /// For the image data contents of a cache entry (may also contain associated metadata)
        /// </summary>
        CacheEntry,
        /// <summary>
        /// For small, non-image-data blobs that contain metadata or other information about a cache entry that should not be evicted as eagerly as the image data.
        /// </summary>
        CacheMetadata,

        /// <summary>
        /// For small blobs that contain metadata or other information about a source image that should generally not be evicted. 
        /// </summary>
        SourceMetadata,
        
        /// <summary>
        /// For essential blobs that should never be evicted, such as index files.
        /// </summary>
        Essential,
    }
}