using System.Collections.Immutable;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;
using Imazen.Routing.HttpAbstractions;

namespace Imazen.Routing.Requests;

public record class BlobResponse(CodeResult<IBlobWrapper> BlobResult) : IAdaptableHttpResponse
{
    public string? ContentType => BlobResult.Value?.Attributes.ContentType ?? "text/plain; charset=utf-8";
    public int StatusCode => BlobResult.IsError ? BlobResult.Error.StatusCode : 200;
    public IImmutableDictionary<string, string>? OtherHeaders { get; init; } //TODO: for errors, no-store and xrobots, for success, 
    // blob attributes
    public async ValueTask WriteAsync<TResponse>(TResponse target, CancellationToken cancellationToken = default) where TResponse : IHttpResponseStreamAdapter
    {
        //TODO: optimize for pipelines?
        
        if (BlobResult.IsError)
        {   
            target.WriteUtf8String(BlobResult.Error.ToString());
        }
        else
        {
            using var blob = BlobResult.Value!.MakeOrTakeConsumable();
            await target.WriteBlobWrapperBody(blob, cancellationToken);
        }
        
    }
    
    public IAdaptableHttpResponse AsResponse() => this;
}

