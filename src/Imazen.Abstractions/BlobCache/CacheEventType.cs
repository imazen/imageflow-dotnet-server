namespace Imazen.Abstractions.BlobCache;

internal enum CacheEventType
{
    YourCacheEntryExpiresSoon,
    ExternalHitYouSkipped,
    ExternalHitYouMissed,
    FreshResultGenerated,
    FreshFileProxied,
    ResultGenerationFailed,
    ResultSourcesNotFound,
    ResultSourcesFailed
    
    
}