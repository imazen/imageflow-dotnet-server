using System.Collections.Immutable;
using System.Text;
using Imazen.Abstractions.Resulting;

namespace Imazen.Routing.HttpAbstractions;

public readonly record struct SmallHttpResponse : IAdaptableReusableHttpResponse
{
    // status code, content type, byte[] content, 
    // headers to set, if any
    
    public required byte[] Content { get; init; }
    public string? ContentType { get; init; }
    public int StatusCode { get; init; }
    public IImmutableDictionary<string,string>? OtherHeaders { get; init; }
    
    public SmallHttpResponse WithHeader(string key, string value)
    {
        return this with { OtherHeaders = (OtherHeaders ?? ImmutableDictionary<string,string>.Empty).Add(key,value) };
    }
    
    
    
    public async ValueTask WriteAsync<TResponse>(TResponse target, CancellationToken cancellationToken = default) where TResponse : IHttpResponseStreamAdapter
    {
        target.SetStatusCode(StatusCode);
        if (ContentType != null)
            target.SetContentType(ContentType);
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (Content != null)
            target.SetContentLength(Content.Length);
        if (OtherHeaders != null)
        {
            foreach (var kv in OtherHeaders)
            {
                target.SetHeader(kv.Key, kv.Value);
            }
        }

        if (Content != null)
        {
            var stream = target.GetBodyWriteStream();
            await stream.WriteAsync(Content, 0, Content.Length, cancellationToken);
        }
    }

    public static SmallHttpResponse Text(int statusCode, string text, IImmutableDictionary<string,string>? otherHeaders = null)
    {
        return new SmallHttpResponse()
        {
            Content = Encoding.UTF8.GetBytes(text),
            ContentType = "text/plain; charset=utf-8",
            StatusCode = statusCode,
            OtherHeaders = otherHeaders
        };
    }
    
    public static SmallHttpResponse NotModified()
    {
        return new SmallHttpResponse()
        {
            StatusCode = 304,
            Content = EmptyBytes,
        };
    }
    private static readonly byte[] EmptyBytes = Array.Empty<byte>();
    
    private static readonly IImmutableDictionary<string,string> NoStoreHeaders = ImmutableDictionary<string,string>.Empty.Add("Cache-Control","no-store");

    private static readonly IImmutableDictionary<string, string> NoStoreNoRobotsHeaders =
        ImmutableDictionary<string, string>.Empty
            .Add("Cache-Control", "no-store").Add("X-Robots-Tag", "none");



    public static SmallHttpResponse NoStore(int statusCode, string text) => NoStore(new HttpStatus(statusCode, text));

    public static SmallHttpResponse NoStore(HttpStatus answer)
        {
        return new SmallHttpResponse()
        {
            Content = Encoding.UTF8.GetBytes(answer.ToString()),
            ContentType = "text/plain; charset=utf-8",
            StatusCode = answer.StatusCode,
            OtherHeaders = NoStoreHeaders
        };
    }
    
    public static SmallHttpResponse NoStoreNoRobots(HttpStatus answer)
    {
        return new SmallHttpResponse()
        {
            Content = Encoding.UTF8.GetBytes(answer.ToString()),
            ContentType = "text/plain; charset=utf-8",
            StatusCode = answer.StatusCode,
            OtherHeaders = NoStoreNoRobotsHeaders
        };
    }
    
    
    
    public static SmallHttpResponse NotFound()
    {
        return new SmallHttpResponse()
        {
            StatusCode = 404,
            Content = EmptyBytes,
        };
    }
    
    public static SmallHttpResponse BadRequest(string text)
    {
        return new SmallHttpResponse()
        {
            Content = Encoding.UTF8.GetBytes(text),
            ContentType = "text/plain; charset=utf-8",
            StatusCode = 400,
        };
    }
    
    public static SmallHttpResponse InternalServerError(string text)
    {
        return new SmallHttpResponse()
        {
            Content = Encoding.UTF8.GetBytes(text),
            ContentType = "text/plain; charset=utf-8",
            StatusCode = 500,
        };
    }
    
    
    
    
    // from bytes
    
    public static SmallHttpResponse FromBytes(int statusCode, string contentType, byte[] content, IImmutableDictionary<string,string>? otherHeaders = null)
    {
        return new SmallHttpResponse()
        {
            Content = content,
            ContentType = contentType,
            StatusCode = statusCode,
            OtherHeaders = otherHeaders
        };
    }
}