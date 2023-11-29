using Imazen.Routing.Helpers;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Promises;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Layers;

public interface IRoutingEndpoint
{
    bool IsBlobEndpoint { get; }
    
    ValueTask<IInstantPromise> GetInstantPromise(IRequestSnapshot request, CancellationToken cancellationToken = default);
}

public class PredefinedResponseEndpoint : IRoutingEndpoint
{
    private readonly IAdaptableReusableHttpResponse response;

    public PredefinedResponseEndpoint(IAdaptableReusableHttpResponse response)
    {
        this.response = response;
    }

    public bool IsBlobEndpoint => false;

    public ValueTask<IInstantPromise> GetInstantPromise(IRequestSnapshot request, CancellationToken cancellationToken = default)
    {
        return Tasks.ValueResult((IInstantPromise)new PredefinedResponsePromise(request, response));
    }
}
public class AsyncEndpointFunc : IRoutingEndpoint
{
    private readonly Func<IRequestSnapshot, CancellationToken, ValueTask<IAdaptableHttpResponse>> func;

    public AsyncEndpointFunc(Func<IRequestSnapshot, CancellationToken, ValueTask<IAdaptableHttpResponse>> func)
    {
        this.func = func;
    }
    public bool IsBlobEndpoint => false;

    public ValueTask<IInstantPromise> GetInstantPromise(IRequestSnapshot request, CancellationToken cancellationToken = default)
    {
        return Tasks.ValueResult<IInstantPromise>(new PromiseFuncAsync(request, func));
    }
    
    private struct AsyncEndpointPromise : IInstantPromise
    {
       private readonly Func<IRequestSnapshot, CancellationToken, ValueTask<IAdaptableHttpResponse>> func;

        public AsyncEndpointPromise(IRequestSnapshot request, Func<IRequestSnapshot, CancellationToken, ValueTask<IAdaptableHttpResponse>> func)
        {
            FinalRequest = request;
            this.func = func;
        }
        
        public bool IsCacheSupporting => false;
        public IRequestSnapshot FinalRequest { get; }

        public ValueTask<IAdaptableHttpResponse> CreateResponseAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline,
            CancellationToken cancellationToken = default)
        {
            return func(FinalRequest, cancellationToken);
        }
    }
}
public class SyncEndpointFunc : IRoutingEndpoint
{
    private readonly Func<IRequestSnapshot, IAdaptableHttpResponse> func;

    public SyncEndpointFunc(Func<IRequestSnapshot, IAdaptableHttpResponse> func)
    {
        this.func = func;
    }
    public bool IsBlobEndpoint => false;
    public ValueTask<IInstantPromise> GetInstantPromise(IRequestSnapshot request, CancellationToken cancellationToken = default)
    {
        return Tasks.ValueResult((IInstantPromise)new PromiseFunc(request, func));
    }
}

public record PromiseWrappingEndpoint(ICacheableBlobPromise Promise) : IRoutingEndpoint
{
    public bool IsBlobEndpoint => true;
    public ValueTask<IInstantPromise> GetInstantPromise(IRequestSnapshot request, CancellationToken cancellationToken = default)
    {
        return Tasks.ValueResult((IInstantPromise)Promise);
    }
}
