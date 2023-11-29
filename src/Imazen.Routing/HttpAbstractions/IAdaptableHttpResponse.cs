using System.Collections.Immutable;

namespace Imazen.Routing.HttpAbstractions;

public interface IAdaptableHttpResponse
{
    string? ContentType { get; }
    int StatusCode { get; }
    IImmutableDictionary<string,string>? OtherHeaders { get; init; }

    ValueTask WriteAsync<TResponse>(TResponse target, CancellationToken cancellationToken = default) where TResponse : IHttpResponseStreamAdapter;
    
}

public interface IAdaptableReusableHttpResponse : IAdaptableHttpResponse
{
}
