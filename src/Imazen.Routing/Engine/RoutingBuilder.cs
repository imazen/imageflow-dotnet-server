using Imazen.Abstractions.Logging;
using Imazen.Abstractions.Resulting;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Layers;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Engine;

public class RoutingBuilder
{
    protected IFastCond? GlobalPreconditions { get; set; }
    protected List<IRoutingLayer> Layers { get; } = new List<IRoutingLayer>();
    // set default conditions (IFastCond)
    // add page endpoints (IRoutingEndpoint)
    
    // Add endpoints based on suffixes or exact matches. At build time, we roll these up into a single FastCond that can be evaluated quickly.

    /// <summary>
    /// Adds an endpoint that also extends GlobalPreconditions
    /// </summary>
    /// <param name="fastMatcher"></param>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    public RoutingBuilder AddGlobalEndpoint(IFastCond fastMatcher, IRoutingEndpoint endpoint)
        => AddEndpoint(true, fastMatcher, endpoint);
    
    public RoutingBuilder AddEndpoint(IFastCond fastMatcher, IRoutingEndpoint endpoint)
        => AddEndpoint(false, fastMatcher, endpoint);
    
    private RoutingBuilder AddEndpoint(bool global, IFastCond fastMatcher, IRoutingEndpoint endpoint)
    {
        if (global) AddAlternateGlobalPrecondition(fastMatcher);
        Layers.Add(new SimpleLayer(fastMatcher.ToString() ?? "(error describing route)",
            (request) => fastMatcher.Matches(request.MutablePath, request.ReadOnlyQueryWrapper) ? 
                CodeResult<IRoutingEndpoint>.Ok(endpoint) : null, fastMatcher));
        return this;
    }
    public RoutingBuilder AddLayer(IRoutingLayer layer)
    {
        Layers.Add(layer);
        return this;
    }
    public RoutingBuilder AddGlobalLayer(IRoutingLayer layer)
    {
        AddAlternateGlobalPrecondition(layer.FastPreconditions ?? Conditions.True);
        Layers.Add(layer);
        return this;
    }
    //Add predefined reusable responses
    
    /// <summary>
    /// Adds an endpoint that also extends GlobalPreconditions
    /// </summary>
    /// <param name="fastMatcher"></param>
    /// <param name="response"></param>
    /// <returns></returns>
    public RoutingBuilder AddGlobalEndpoint(IFastCond fastMatcher, IAdaptableReusableHttpResponse response)
    {
        var endpoint = new PredefinedResponseEndpoint(response);
        return AddGlobalEndpoint(fastMatcher, endpoint);
    }
    public RoutingBuilder AddEndpoint(IFastCond fastMatcher, IAdaptableReusableHttpResponse response)
    {
        var endpoint = new PredefinedResponseEndpoint(response);
        return AddEndpoint(fastMatcher, endpoint);
    }
    /// <summary>
    /// Adds an endpoint that also extends GlobalPreconditions
    /// </summary>
    /// <param name="fastMatcher"></param>
    /// <param name="handler"></param>
    /// <returns></returns>
    public RoutingBuilder AddGlobalEndpoint(IFastCond fastMatcher, Func<IRequestSnapshot, IAdaptableHttpResponse> handler)
    {
        var endpoint = new SyncEndpointFunc(handler);
        return AddGlobalEndpoint(fastMatcher, endpoint);
    }
    
    public RoutingBuilder AddEndpoint(IFastCond fastMatcher, Func<IRequestSnapshot, IAdaptableHttpResponse> handler)
    {
        var endpoint = new SyncEndpointFunc(handler);
        return AddEndpoint(fastMatcher, endpoint);
    }
    
    // Set default Preconditions IFastCond
    
    
    public RoutingBuilder SetGlobalPreconditions(IFastCond fastMatcher)
    {
        GlobalPreconditions = fastMatcher;
        return this;
    }
    public IFastCond CreatePreconditionToRequireImageExtensionOrExtensionlessPathPrefixes<T>(IReadOnlyCollection<T>? extensionlessPaths = null)
        where T : IStringAndComparison
    {
        if (extensionlessPaths is { Count: > 0 })
        {
            return Conditions.HasSupportedImageExtension.Or(
                Conditions.PathHasNoExtension.And(Conditions.HasPathPrefix(extensionlessPaths)));
        }
        return Conditions.HasSupportedImageExtension;
    }
    
    public IFastCond CreatePreconditionToRequireImageExtensionOrSuffixOrExtensionlessPathPrefixes<T>(IReadOnlyCollection<T>? extensionlessPaths = null, string[]? alternatePathSuffixes = null)
        where T : IStringAndComparison
    {
        if (alternatePathSuffixes is { Length: > 0 })
        {
            return CreatePreconditionToRequireImageExtensionOrExtensionlessPathPrefixes(extensionlessPaths)
                .Or(Conditions.HasPathSuffixOrdinalIgnoreCase(alternatePathSuffixes));
        }
        return CreatePreconditionToRequireImageExtensionOrExtensionlessPathPrefixes(extensionlessPaths);
    }
    
    public RoutingBuilder AddAlternateGlobalPrecondition(IFastCond? fastMatcher)
    {
        if (fastMatcher == null) return this;
        if (fastMatcher is Conditions.FastCondFalse) return this;
        GlobalPreconditions = GlobalPreconditions?.Or(fastMatcher) ?? fastMatcher;
        return this;
    }
    
    public RoutingEngine Build(IReLogger logger)
    {
        return new RoutingEngine(Layers.ToArray(), GlobalPreconditions ?? Conditions.True, logger);
    }
}