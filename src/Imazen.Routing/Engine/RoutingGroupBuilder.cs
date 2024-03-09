using Imazen.Abstractions.Resulting;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Layers;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Engine;


public sealed class RoutingGroupBuilder
{
    public RoutingGroupBuilder(string name)
    {
        Name = name;
    }
    
    public string Name { get; internal set; }
    
    public RoutingGroupPreconditionsBuilder Preconditions { get; } = new();
    
    public RoutingGroupBuilder ConfigurePreconditions(Action<RoutingGroupPreconditionsBuilder> configure)
    {
        configure(Preconditions);
        return this;
    }

    public RoutingGroupBuilder SetGroupName(string name)
    {
        Name = name;
        return this;
    }
 
    private List<IRoutingLayer> Layers { get; } = new();
    

    public RoutingLayerGroup Build()
    {
        var preconditions = Preconditions.ToOptimizedCondition();

        var everyLayerHasConditions = Layers.All(l => l.FastPreconditions.HasConditions());
        
        
        var recursiveComputed = !everyLayerHasConditions ? preconditions :
            preconditions.And(Layers.Select(l => l.FastPreconditions!).AnyPrecondition()).Optimize();
            
        
        return new()
        {
            Name = Name,
            GroupPrecondition = preconditions,
            Layers = Layers,
            RecursiveComputedConditions = recursiveComputed
        };
    }

    private RoutingGroupBuilder AddEndpoint(IFastCond fastMatcher, IRoutingEndpoint endpoint)
    {
        Layers.Add(new SimpleLayer(fastMatcher.ToString() ?? "(error describing route)",
            (request) => fastMatcher.Matches(request.MutablePath, request.ReadOnlyQueryWrapper) ? 
                CodeResult<IRoutingEndpoint>.Ok(endpoint) : null, fastMatcher));
        return this;
    }
    public RoutingGroupBuilder AddLayer(IRoutingLayer layer)
    {
        Layers.Add(layer);
        return this;
    }
    
    public RoutingGroupBuilder AddEndpoint(IFastCond fastMatcher, IAdaptableReusableHttpResponse response)
    {
        var endpoint = new PredefinedResponseEndpoint(response);
        return AddEndpoint(fastMatcher, endpoint);
    }
    public RoutingGroupBuilder AddEndpoint(IFastCond fastMatcher, Func<IRequestSnapshot, IAdaptableHttpResponse> handler)
    {
        var endpoint = new SyncEndpointFunc(handler);
        return AddEndpoint(fastMatcher, endpoint);
    }
    
 
}