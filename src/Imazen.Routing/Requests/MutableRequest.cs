using System.Buffers;
using System.Text;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.HttpStrings;
using Imazen.Routing.HttpAbstractions;
using Microsoft.Extensions.Primitives;

namespace Imazen.Routing.Requests;

/// <summary>
/// Mutable request object for routing layers to manipulate.
/// </summary>
public class MutableRequest : IRequestSnapshot
{
    private MutableRequest(IHttpRequestStreamAdapter originatingRequest, bool isChildRequest, string? childRequestUri, IRequestSnapshot? parent)
    {
        OriginatingRequest = originatingRequest;
        IsChildRequest = isChildRequest;
        ChildRequestUri = childRequestUri;
        ParentRequest = parent;
        MutablePath = originatingRequest.GetPath().Value ?? "";
        
#if DOTNET5_0_OR_GREATER
        MutableQueryString = new Dictionary<string, StringValues>(originatingRequest.GetQuery(), StringComparer.OrdinalIgnoreCase);
#else
        MutableQueryString = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in originatingRequest.GetQuery())
        {
            MutableQueryString.Add(kv.Key, kv.Value);
        }
#endif
    }
    
    
    public static MutableRequest OriginalRequest(IHttpRequestStreamAdapter originalRequest)
    {
        return new MutableRequest(originalRequest, false, null, null)
        {
            HttpMethod = originalRequest.Method,

        };

    }
    public static MutableRequest ChildRequest(IHttpRequestStreamAdapter originalRequest, IRequestSnapshot parent, string childRequestUri, string httpMethod)
    {
        return new MutableRequest(originalRequest, true, childRequestUri, parent){
            HttpMethod = httpMethod,
            MutablePath = childRequestUri
        };
    }
    public IHttpRequestStreamAdapter? OriginatingRequest { get; }
    
    public IHttpRequestStreamAdapter UnwrapOriginatingRequest()
    {
        if (OriginatingRequest == null)
        {
            throw new InvalidOperationException("OriginatingRequest is required, but was null");
        }
        return OriginatingRequest;
    }
    public required string HttpMethod { get; set; }
    
    public bool IsChildRequest { get; }
    public string? ChildRequestUri { get; }
    public IRequestSnapshot? ParentRequest { get; }
    public IDictionary<string, StringValues>? QueryString => MutableQueryString;

    /// <summary>
    /// Modifies the path in escaped form
    /// </summary>
    public string MutablePath { get; set; }
    public IDictionary<string, StringValues> MutableQueryString { get; set; }
    
    
    private DictionaryQueryWrapper? finalQuery;

    public IReadOnlyQueryWrapper ReadOnlyQueryWrapper
    {
        get
        {
            if (finalQuery == null || !object.ReferenceEquals(finalQuery.UnderlyingDictionary, MutableQueryString))
            {
                finalQuery = new DictionaryQueryWrapper(MutableQueryString);
            }
            return finalQuery;
        }
    }

    public bool MutationsComplete { get; }
    public string Path => MutablePath;
    public IDictionary<string, string>? ExtractedData { get; set; }
    public ICollection<SearchableBlobTag>? StorageTags { get; set; }
    
    public IRequestSnapshot ToSnapshot(bool mutationsComplete)
    {
        return new RequestSnapshot(mutationsComplete){
            Path = MutablePath,
            QueryString = MutableQueryString,
            ExtractedData = ExtractedData,
            StorageTags = StorageTags,
            ParentRequest = ParentRequest,
            HttpMethod = HttpMethod,
            MutationsComplete = mutationsComplete,
            OriginatingRequest = OriginatingRequest
        };
    }

    public void WriteCacheKeyBasisPairsTo(IBufferWriter<byte> writer)
    {
        writer.WriteWtf16String(this.HttpMethod);
        writer.WriteWtf16PathAndRouteValuesCacheKeyBasis(this);
    }
    
    internal static void PrintDictionary<TK,TV>(StringBuilder sb, IEnumerable<KeyValuePair<TK, TV>> dict) 
    {
        sb.Append("{");
        foreach (var kv in dict)
        {
            sb.Append(kv.Key);
            sb.Append("=");
            sb.Append(kv.Value);
            sb.Append(",");
        }
        sb.Append("}");
    }
    
    internal static string RequestToString(IRequestSnapshot request)
    {
        var sb = new StringBuilder();
        sb.Append(request.HttpMethod);
        sb.Append(" ");
        sb.Append(request.QueryString == null ? request.Path : QueryHelpers.AddQueryString(request.Path, request.QueryString));
        // now add ExtractedData, StorageTags, ParentRequest, and MutationsComplete
        // in the form {Name='values', Name2='values2'}
        sb.Append(" {");
        if (request.ExtractedData != null)
        {
            PrintDictionary<string,string>(sb, request.ExtractedData);
            sb.Append(", ");
        }
        if (request.StorageTags != null)
        {
            PrintDictionary(sb, request.StorageTags.Select(t => t.ToKeyValuePair()));
            sb.Append(", ");
        }
        if (request.ParentRequest != null)
        {
            sb.Append("ParentRequest: ");
            sb.Append(request.ParentRequest);
            sb.Append(", ");
        }
        if (request.OriginatingRequest != null)
        {
            sb.Append("OriginatingRequest: ");
            sb.Append(request.OriginatingRequest.ToShortString());
            sb.Append(", ");
        }
        sb.Append("MutationsComplete: ");
        sb.Append(request.MutationsComplete);
        sb.Append("}");
        return sb.ToString();
    }
    
    public override string ToString()
    {
        return RequestToString(this);
    }
}

