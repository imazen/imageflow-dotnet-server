using System.Text;
using Imazen.Abstractions.Logging;
using Imazen.Abstractions.Resulting;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Layers;
using Imazen.Routing.Promises;
using Imazen.Routing.Requests;
using Imazen.Routing.Serving;
using Microsoft.Extensions.Logging;

namespace Imazen.Routing.Engine;

public class RoutingEngine : IBlobRequestRouter, IHasDiagnosticPageSection
{
    private readonly IRoutingLayer[] layers;
    private readonly IReLogger logger;

    private readonly IFastCond mightHandleConditions; 
    internal RoutingEngine(IRoutingLayer[] layers, IFastCond globalPrecondition, IReLogger logger)
    {
        this.layers = layers;
        this.logger = logger;
        
        // Build a list of unique fast exits we must consult. We can skip duplicates.
        var preconditions = new List<IFastCond>(layers.Length) { };
        foreach (var layer in layers)
        {
            if (layer.FastPreconditions != null && !preconditions.Contains(layer.FastPreconditions))
            {
                preconditions.Add(layer.FastPreconditions);
            }
        }
        mightHandleConditions = globalPrecondition.And(preconditions.AnyPrecondition()).Optimize();
    }

    public bool MightHandleRequest<TQ>(string path, TQ query) where TQ : IReadOnlyQueryWrapper
    {
        return mightHandleConditions.Matches(path, query);
    }

    public async ValueTask<CodeResult<IRoutingEndpoint>?> Route(MutableRequest request, CancellationToken cancellationToken = default)
    {
        // log info about the request
        foreach (var layer in layers)
        {
            var result = await layer.ApplyRouting(request,cancellationToken);
            // log what (if anything) has changed.
            if (result != null)
            {
                // log the result
                return result;
            }
        }

        return null; // We don't have matching routing for this. Let the rest of the app handle it.
    }
    

    public async ValueTask<CodeResult<ICacheableBlobPromise>?> RouteToPromiseAsync(MutableRequest request, CancellationToken cancellationToken = default)
    {
        // log info about the request
        foreach (var layer in layers)
        {
            var result = await layer.ApplyRouting(request,cancellationToken);
            // log what (if anything) has changed.
            if (result != null)
            {
                
                if (result.IsOk)
                {
                    var endpoint = result.Value;
                    if (endpoint!.IsBlobEndpoint)
                    {
                        var promise = await endpoint.GetInstantPromise(request, cancellationToken);
                        if (promise.IsCacheSupporting && promise is ICacheableBlobPromise cacheablePromise)
                        {
                            return CodeResult<ICacheableBlobPromise>.Ok(cacheablePromise);
                        }
                    }
                    else
                    {
                        logger.LogError("Imageflow Routing endpoint {0} (from routing layer {1} is not a blob endpoint",
                            endpoint, layer.Name);
                        return CodeResult<ICacheableBlobPromise>.Err(
                            HttpStatus.ServerError.WithAppend("Routing endpoint is not a blob endpoint"));
                    }
                }
                else
                {
                    return CodeResult<ICacheableBlobPromise>.Err(result.Error);
                }
            }
        }

        return null; // We don't have matching routing for this. Let the rest of the app handle it.
    }

    public string? GetDiagnosticsPageSection(DiagnosticsPageArea section)
    {
        if (section == DiagnosticsPageArea.Start)
        {
            // preconditions
            var sb = new StringBuilder();
            sb.AppendLine($"Routing Engine: {layers.Length} layers, Preconditions: {mightHandleConditions}");
            for(var i = 0; i < layers.Length; i++)
            {
                sb.AppendLine($"layer[{i}]: {layers[i].Name} ({layers[i].GetType().Name}), Preconditions={layers[i].FastPreconditions}");
            }
            return sb.ToString();
        }
        else
        {
            
        }
        return null;
    }
}