using Imazen.Abstractions.Resulting;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Layers;

public interface IRoutingLayer
{
    string Name { get; }
    
    IFastCond? FastPreconditions { get; }
    ValueTask<CodeResult<IRoutingEndpoint>?> ApplyRouting(MutableRequest request, CancellationToken cancellationToken = default);
}
