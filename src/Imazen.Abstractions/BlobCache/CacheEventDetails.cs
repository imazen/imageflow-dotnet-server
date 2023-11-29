using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;

namespace Imazen.Abstractions.BlobCache
{
    /// <summary>
    /// TODO: clean this up and minimize it once we have implementations of the 3 caches
    /// Currently no usage to drive refinement
    /// </summary>
    public interface ICacheEventDetails
    {
        BlobGroup BlobCategory { get; }
        IReusableBlobFactory BlobFactory { get; }
        /// <summary>
        /// The following cache responded with a hit
        /// </summary>
        IBlobCache? ExternalCacheHit { get; }
        /// <summary>
        /// A fresh result was generated and was stored in Result
        /// </summary>
        bool FreshResultGenerated { get; }
        
        /// <summary>
        /// Generation of this result failed. Brief failure caching may be appropriate to conserve server resources.
        /// But is best left to the main cache engine.  
        /// </summary>
        bool GenerationFailed { get; }
        IBlobCacheRequest OriginalRequest { get; }
        IResult<IBlobWrapper,IBlobCacheFetchFailure>? Result { get; }
        DateTimeOffset AsyncWriteJobCreatedAt { get; set; }
        TimeSpan FreshResultGenerationTime { get; set; }

        /// <summary>
        /// If true, we are holding up image delivery.
        /// </summary>
        bool InServerRequest { get; set; }
    }
    
    public record CacheEventDetails : ICacheEventDetails
    {
        public BlobGroup BlobCategory => OriginalRequest.BlobCategory;
        public required IReusableBlobFactory BlobFactory { get; init; }
        public IBlobCache? ExternalCacheHit { get; init; }
        public bool FreshResultGenerated { get; init; }
        public bool GenerationFailed { get; init; }
        public required IBlobCacheRequest OriginalRequest { get; init; }
        public CacheFetchResult? Result { get; init; }
        public DateTimeOffset AsyncWriteJobCreatedAt { get; set; }
        public TimeSpan FreshResultGenerationTime { get; set; }
        public bool InServerRequest { get; set; }
        
        public static CacheEventDetails Create(IBlobCacheRequest request, 
            IReusableBlobFactory blobFactory, 
            IBlobCache? externalCacheHit,
            bool freshResultGenerated, bool generationFailed, CacheFetchResult? result)
        {
            return new CacheEventDetails
            {
                OriginalRequest = request,
                BlobFactory = blobFactory,
                ExternalCacheHit = externalCacheHit,
                FreshResultGenerated = freshResultGenerated,
                GenerationFailed = generationFailed,
                Result = result,
                AsyncWriteJobCreatedAt = DateTimeOffset.UtcNow
            };
        }
        
        public static CacheEventDetails CreateFreshResultGeneratedEvent(IBlobCacheRequest request, 
            IReusableBlobFactory blobFactory, 
            CacheFetchResult? result)
        {
            return new CacheEventDetails
            {
                OriginalRequest = request,
                BlobFactory = blobFactory,
                ExternalCacheHit = null,
                FreshResultGenerated = true,
                GenerationFailed = result?.IsError ?? false,
                Result = result,
                //TODO - this may not be correct
                AsyncWriteJobCreatedAt = DateTimeOffset.UtcNow
            };
        }
        
        public static CacheEventDetails CreateExternalHitEvent(IBlobCacheRequest request, IBlobCache externalCache,
            IReusableBlobFactory blobFactory, 
            CacheFetchResult? result)
        {
            return new CacheEventDetails
            {
                OriginalRequest = request,
                BlobFactory = blobFactory,
                ExternalCacheHit = externalCache,
                FreshResultGenerated = false,
                GenerationFailed = result?.IsError ?? false,
                Result = result,
                //TODO - this may not be correct
                AsyncWriteJobCreatedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
