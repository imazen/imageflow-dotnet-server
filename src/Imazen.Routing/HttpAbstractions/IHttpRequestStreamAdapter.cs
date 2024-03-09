using System.IO.Pipelines;
using System.Net;
using System.Text;
using Imazen.Abstractions.HttpStrings;
using Microsoft.Extensions.Primitives;

namespace Imazen.Routing.HttpAbstractions;

/// <summary>
/// 
/// 
/// </summary>
public interface IHttpRequestStreamAdapter
{
    // Microsoft.AspNetCore.Http.HttpRequest and System.Web.HttpRequest use different types for their properties.
    // PathString, QueryString, and PathBase are all strings in System.Web.HttpRequest, but are structs in Microsoft.AspNetCore.Http.HttpRequest.
    // https://source.dot.net/#Microsoft.AspNetCore.Http.Abstractions/PathString.cs,ff3b073b1591ea45
    // https://source.dot.net/#Microsoft.AspNetCore.Http.Abstractions/HostString.cs,677c1978743f8e43
    // https://source.dot.net/#Microsoft.AspNetCore.Http.Abstractions/QueryString.cs,b704925bb788f6c6


    /// <summary>
    /// May be empty if PathBase contains the full path (like for the doc root).
    /// Path should be fully decoded except for '%2F' which translates to / and would change path parsing.
    /// </summary>
    /// <returns></returns>

    string? TryGetServerVariable(string key);
    
    UrlPathString GetPath();
    
    /// <summary>
    /// Should not end with a trailing slash. If the app is mounted in a subdirectory, this will contain that part of the path.
    /// If no subdir mounting is supported, should return String.Empty.
    /// </summary>
    /// <returns></returns>

    UrlPathString GetPathBase();
    
    Uri GetUri();
    
    UrlQueryString GetQueryString();
    UrlHostString GetHost();
    
    IEnumerable<KeyValuePair<string, string>>? GetCookiePairs();
    IDictionary<string, StringValues> GetHeaderPairs();
    IReadOnlyQueryWrapper GetQuery();
    bool TryGetHeader(string key, out StringValues value);
    bool TryGetQueryValues(string key, out StringValues value);
  
    
    /// <summary>
    /// Avoid using this if possible.
    /// It's a hack to get around the fact that HttpContext is not a public interface and this
    /// interface is used in both non-web contexts and situations where HttpContext may or may not be availble
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T? GetHttpContextUnreliable<T>() where T : class;
   
    string? ContentType { get; }
    long? ContentLength { get; }
    string Protocol { get; }
    string Scheme { get; }
    string Method { get; }
    bool IsHttps { get; }
    
    bool SupportsStream { get; }
    Stream GetBodyStream();
    
    IPAddress? GetRemoteIpAddress();
    IPAddress? GetLocalIpAddress();
    
    
    bool SupportsPipelines { get; }
    PipeReader GetBodyPipeReader();

}

// extension helper for local request
public static class HttpRequestStreamAdapterExtensions
{
    public static string ToShortString(this IHttpRequestStreamAdapter request)
    {
        // GET /path?query HTTP/1.1
        // Host: example.com:443
        // {count} Cookies
        // {count} Headers
        // Referer: example.com
        // Supports=pipelines,streams
        
        var sb = new StringBuilder();
        sb.Append(request.Method);
        sb.Append(" ");
        sb.Append(request.GetPathBase().Value);
        sb.Append(request.GetPath().Value);
        sb.Append(request.GetQueryString());
        sb.Append(" ");
        sb.Append(request.Protocol);
        sb.Append("\r\n");
        sb.Append("Host: ");
        sb.Append(request.GetHost());
        sb.Append("\r\n");
        var cookies = request.GetCookiePairs();
        if (cookies != null)
        {
            sb.Append(cookies.Count());
            sb.Append(" Cookies\r\n");
        }
        var headers = request.GetHeaderPairs();
        sb.Append(headers.Count);
        sb.Append(" Headers\r\n");
        foreach (var header in headers)
        {
            sb.Append(header.Key);
            sb.Append(": ");
            sb.Append(header.Value);
            sb.Append("\r\n");
        }
        sb.Append("[Supports=");
        if (request.SupportsPipelines)
        {
            sb.Append("PipeReader,");
        }
        if (request.SupportsStream)
        {
            sb.Append("Stream");
        }
        sb.Append("]");
        return sb.ToString();
    }
    
    public static string GetServerHost(this IHttpRequestStreamAdapter request)
    {
        var host = request.GetHost();
        return host.HasValue ? host.Value : "localhost";
    }
    public static string? GetRefererHost(this IHttpRequestStreamAdapter request)
    {
        if (request.GetHeaderPairs().TryGetValue("Referer", out var refererValues))
        {
            if (refererValues.Count > 0)
            {
                var referer = refererValues.ToString();
                if (!string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var result))
                {
                    return result.DnsSafeHost;
                }
            }
                
        }
        return null;
    }
    public static bool IsClientLocalhost(this IHttpRequestStreamAdapter request)
    {
        // What about when a dns record is pointing to the local machine?
        var serverIp = request.GetLocalIpAddress();
        if (serverIp == null)
        {
            return false;
        }
        var clientIp = request.GetRemoteIpAddress();
        if (clientIp == null)
        {
            return false;
        }
        
        if (IPAddress.IsLoopback(clientIp))
        {
            return true;
        }
        // if they're the same
        if (serverIp.Equals(clientIp))
        {
            return true;
        }
        return false;
    }
}
