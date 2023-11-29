using System.Text;

namespace Imazen.Abstractions.BlobCache;

/// <summary>
/// We don't use this and can delete it. Interfaces over records make it very clumsy to use.
/// </summary>
internal interface IBlobCacheCapabilities
{
    bool CanFetchMetadata { get; }
    bool CanFetchData { get; }
    bool CanConditionalFetch { get; }
    bool CanPut { get; }
    bool CanConditionalPut { get; }
    bool CanDelete { get; }
    bool CanSearchByTag { get; }
    bool CanPurgeByTag { get; }
    bool CanReceiveEvents { get; }
    bool SupportsHealthCheck { get; }
    bool SubscribesToRecentRequest { get; }
    bool SubscribesToExternalHits { get; }
    bool SubscribesToFreshResults { get; }
    
    bool RequiresInlineExecution { get;}
    bool FixedSize { get; }
}

public record BlobCacheCapabilities: IBlobCacheCapabilities
{
    public required bool CanFetchMetadata { get; init; }
    public required bool CanFetchData { get; init; }
    public required bool CanConditionalFetch { get; init; }
    public required bool CanPut { get; init; }
    public required bool CanConditionalPut { get; init; }
    public required bool CanDelete { get; init; }
    public required bool CanSearchByTag { get; init; }
    public required bool CanPurgeByTag { get; init; }
    public required bool CanReceiveEvents { get; init; }
    public required bool SupportsHealthCheck { get; init; }
    public required bool SubscribesToRecentRequest { get; init; }
    public required bool SubscribesToExternalHits { get; init; }
    
    public required bool SubscribesToFreshResults { get; init; }
    
    public required bool RequiresInlineExecution { get; init; }
    public required bool FixedSize { get; init; }

    public void WriteTruePairs(StringBuilder sb)
    {
        if (CanFetchData) sb.Append("CanFetchData, ");
        if (CanFetchMetadata) sb.Append("CanFetchMetadata, ");
        if (CanConditionalFetch) sb.Append("CanConditionalFetch, ");
        if (CanPut) sb.Append("CanPut, ");
        if (CanConditionalPut) sb.Append("CanConditionalPut, ");
        if (CanDelete) sb.Append("CanDelete, ");
        if (CanSearchByTag) sb.Append("CanSearchByTag, ");
        if (CanPurgeByTag) sb.Append("CanPurgeByTag, ");
        if (CanReceiveEvents) sb.Append("CanReceiveEvents, ");
        if (SupportsHealthCheck) sb.Append("SupportsHealthCheck, ");
        if (SubscribesToRecentRequest) sb.Append("SubscribesToRecentRequest, ");
        if (SubscribesToExternalHits) sb.Append("SubscribesToExternalHits, ");
        if (SubscribesToFreshResults) sb.Append("SubscribesToFreshResults, ");
        if (RequiresInlineExecution) sb.Append("RequiresInlineExecution, ");
        if (FixedSize) sb.Append("FixedSize, ");
    }

    public void WriteReducedPairs(StringBuilder sb, BlobCacheCapabilities subtract)
    {
        if (subtract.CanFetchData && !CanFetchData) sb.Append("-CanFetchData, ");
        if (subtract.CanFetchMetadata && !CanFetchMetadata) sb.Append("-CanFetchMetadata, ");
        if (subtract.CanConditionalFetch && !CanConditionalFetch) sb.Append("-CanConditionalFetch, ");
        if (subtract.CanPut && !CanPut) sb.Append("-CanPut, ");
        if (subtract.CanConditionalPut && !CanConditionalPut) sb.Append("-CanConditionalPut, ");
        if (subtract.CanDelete && !CanDelete) sb.Append("-CanDelete, ");
        if (subtract.CanSearchByTag && !CanSearchByTag) sb.Append("-CanSearchByTag, ");
        if (subtract.CanPurgeByTag && !CanPurgeByTag) sb.Append("-CanPurgeByTag, ");
        if (subtract.CanReceiveEvents && !CanReceiveEvents) sb.Append("-CanReceiveEvents, ");
        if (subtract.SupportsHealthCheck && !SupportsHealthCheck) sb.Append("-SupportsHealthCheck, ");
        if (subtract.SubscribesToRecentRequest && !SubscribesToRecentRequest) sb.Append("-SubscribesToRecentRequest, ");
        if (subtract.SubscribesToExternalHits && !SubscribesToExternalHits) sb.Append("-SubscribesToExternalHits, ");
        if (subtract.SubscribesToFreshResults && !SubscribesToFreshResults) sb.Append("-SubscribesToFreshResults, ");
        if (subtract.RequiresInlineExecution && !RequiresInlineExecution) sb.Append("-RequiresInlineExecution, ");
        if (subtract.FixedSize && !FixedSize) sb.Append("-FixedSize, ");
        
    }


    public static readonly BlobCacheCapabilities None = new BlobCacheCapabilities
    {
        CanFetchMetadata = false,
        CanFetchData = false,
        CanConditionalFetch = false,
        CanPut = false,
        CanConditionalPut = false,
        CanDelete = false,
        CanSearchByTag = false,
        CanPurgeByTag = false,
        CanReceiveEvents = false,
        SupportsHealthCheck = false,
        SubscribesToRecentRequest = false,
        SubscribesToExternalHits = false,
        FixedSize = false,
        RequiresInlineExecution = false,
        SubscribesToFreshResults = false
    };

    public static readonly BlobCacheCapabilities OnlyHealthCheck = None with {SupportsHealthCheck = true};

}