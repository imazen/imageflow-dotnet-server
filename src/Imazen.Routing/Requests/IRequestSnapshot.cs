using System.Buffers;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.HttpStrings;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Promises;
using Microsoft.Extensions.Primitives;

namespace Imazen.Routing.Requests;

/// <summary>
/// This intentionally does not include a querystring -
/// Vary-By data should be placed into RouteValues and AddCacheBasisItemsTo() should normalize the cache data into a StringBuilder.
/// </summary>
public interface IGetResourceSnapshot : IHasCacheKeyBasis
{
    /// <summary>
    /// If all required mutations have been applied to the resource, (such as rewriting, parsing of routing values, adding querystring defaults, etc)
    /// </summary>
    bool MutationsComplete { get; }
    
    /// <summary>
    /// Gets the rewritten path to the resource. 
    /// </summary>
    /// <value>
    /// A <see cref="string"/> representing the rewritten path.
    /// </value>
    string Path { get; }
    
    
    /// <summary>
    /// Key/value pairs parsed from the Imageflow routing system. Often 'container' and 'key' are present.
    /// If host values or CDN forwarded headers need to be considered, then the <see cref="IHttpRequestStreamAdapter"/> should be used to extract
    /// and store that data here.
    /// </summary>
    ///
    // TODO: This is mutable! this is an immutable interface!
    IDictionary<string, string>? ExtractedData { get; } // TODO: we need to be able to extract data that doesn't factor into the cache key, so we probably need another dict.
    // And maybe we should keep resource data separate from processing commands?
    
  
    /// <summary>
    /// Tag pairs specified here will be stored alongside cached copies and can be searched/purcged by matching pairs.
    /// </summary>
    ICollection<SearchableBlobTag>? StorageTags { get; }

}

public interface IRequestSnapshot : IGetResourceSnapshot
{
    
    public IHttpRequestStreamAdapter? OriginatingRequest { get; }
    
    string HttpMethod { get; }
    
    /// <summary>
    /// When merging multiple images (such as applying a watermark), the parent request is the original request.
    /// Only the request for the watermark file will have a parent.
    /// </summary>
    IRequestSnapshot? ParentRequest { get; }
    
    /// <summary>
    /// Typically, this should only be used for image processing commands, and shouldn't indicate the primary resource in any way.
    /// </summary>
    IDictionary<string, StringValues>? QueryString { get; }

}
public record RequestSnapshot(bool MutationsComplete) : IRequestSnapshot{
    
    public IHttpRequestStreamAdapter? OriginatingRequest { get; init; }
    public required string Path { get; init; }
    public IDictionary<string, StringValues>? QueryString { get; init;}
    public IDictionary<string, string>? ExtractedData { get; init;}
    public ICollection<SearchableBlobTag>? StorageTags { get; init;}
    public IRequestSnapshot? ParentRequest { get; init; }
    
    public required string HttpMethod { get; init; } = HttpMethodStrings.Get;
    public void WriteCacheKeyBasisPairsTo(IBufferWriter<byte> writer)
    {
        if (!MutationsComplete) throw new InvalidOperationException("Mutations must be complete before writing cache key basis pairs");
        
        // TODO: write all pairs using memcasting and wtf16
    }
    
    public override string ToString()
    {
        return MutableRequest.RequestToString(this);
    }
}



public static class RequestSnapshotExtensions
{
    public static bool IsGet(this IRequestSnapshot request)
    {
        return request.HttpMethod == HttpMethodStrings.Get;
    }
    
    // Write 
}