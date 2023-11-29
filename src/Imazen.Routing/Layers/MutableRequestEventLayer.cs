using Imazen.Abstractions.Resulting;
using Imazen.Routing.Promises.Pipelines.Watermarking;
using Imazen.Routing.Helpers;
using Imazen.Routing.Requests;
using Microsoft.Extensions.Primitives;

namespace Imazen.Routing.Layers;
internal class PathPrefixHandler<T>
{
    internal PathPrefixHandler(string prefix, T handler)
    {
        PathPrefix = prefix;
        Handler = handler;
    }
    public string PathPrefix { get; }
        
    public T Handler { get; }
}

internal readonly struct MutableRequestEventArgs
{
    internal MutableRequestEventArgs(MutableRequest request)
    {
        Request = request;
    }
    public string VirtualPath {
        get => Request.MutablePath;
        set => Request.MutablePath = value;
    }
    public MutableRequest Request { get; }
        
    public IDictionary<string,StringValues> Query {
        get => Request.MutableQueryString;
        set => Request.MutableQueryString = value;
    }
}
internal class MutableRequestEventLayer: IRoutingLayer
{
    public MutableRequestEventLayer(string name, List<PathPrefixHandler<Func<MutableRequestEventArgs, bool>>> handlers)
    {
        Name = name;
        Handlers = handlers;
        if (Handlers.Any(x => string.IsNullOrEmpty(x.PathPrefix)))
        {
            FastPreconditions = new Conditions.FastCondTrue();
        }
        else
        {
            FastPreconditions = new Conditions.FastCondHasPathPrefixes(Handlers.Select(x => x.PathPrefix).ToArray(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
    public string Name { get; }
    public List<PathPrefixHandler<Func<MutableRequestEventArgs, bool>>> Handlers { get; }
    public IFastCond? FastPreconditions { get; }
    public ValueTask<CodeResult<IRoutingEndpoint>?> ApplyRouting(MutableRequest request, CancellationToken cancellationToken = default)
    {
        var args = new MutableRequestEventArgs(request);
        
        foreach (var handler in Handlers)
        {
            var matches = string.IsNullOrEmpty(handler.PathPrefix) ||
                          request.Path.StartsWith(handler.PathPrefix, StringComparison.OrdinalIgnoreCase);
            if (matches && !handler.Handler(args))
                return
                    Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>
                        (CodeResult<IRoutingEndpoint>.Err((403, "Forbidden")));
        }
        return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null);
    }
    
    /// ToString includes all data in the layer, for full diagnostic transparency, and lists the preconditions and data count
    public override string ToString()
    {
        return $"Routing Layer {Name}: {Handlers.Count} handlers, Preconditions: {FastPreconditions}";
    }
}