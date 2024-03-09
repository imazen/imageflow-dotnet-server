namespace Imazen.Routing.Layers;

public record RoutingLayerGroup
{
    public required string Name { get; init; }
    
    public required IFastCond? GroupPrecondition { get; init; }
    
    /// <summary>
    /// A massive condition that is used to determine if any layer in the group matches.
    /// </summary>
    public required IFastCond RecursiveComputedConditions { get; init; }
    
    public required IReadOnlyList<IRoutingLayer> Layers { get; init; }
}