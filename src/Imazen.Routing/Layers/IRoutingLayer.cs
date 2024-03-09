using Imazen.Abstractions.Resulting;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Layers;

public interface IRoutingLayer
{
    /// <summary>
    /// For debugging and logging purposes.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// These preconditions are used to quickly determine if a layer can be applied to a request.
    /// </summary>
    IFastCond? FastPreconditions { get; }
    
    /// <summary>
    /// This will only be called if FastPreconditions is null or evaluates to true.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<CodeResult<IRoutingEndpoint>?> ApplyRouting(MutableRequest request, CancellationToken cancellationToken = default);
}
