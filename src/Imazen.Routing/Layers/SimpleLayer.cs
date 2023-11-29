using Imazen.Abstractions.Resulting;
using Imazen.Routing.Helpers;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Layers;

internal record SimpleLayer(string Name, Func<MutableRequest, CodeResult<IRoutingEndpoint>?> Function, IFastCond? FastPreconditions) : IRoutingLayer
{
    public ValueTask<CodeResult<IRoutingEndpoint>?> ApplyRouting(MutableRequest request, CancellationToken cancellationToken = default)
    {
        return Tasks.ValueResult(Function(request));
    }
}