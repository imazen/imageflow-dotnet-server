using Imazen.Abstractions.Resulting;
using Imazen.Routing.Helpers;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Layers;

public record struct CommandDefaultsLayerOptions(bool ApplyDefaultCommandsToQuerylessUrls, Dictionary<string, string> CommandDefaults);

public class CommandDefaultsLayer : IRoutingLayer
{
    private CommandDefaultsLayerOptions options;

    public CommandDefaultsLayer(CommandDefaultsLayerOptions options)
    {
        this.options = options;
        if (options.CommandDefaults.Count == 0)
        {
            FastPreconditions = null;
        }else if (this.options.ApplyDefaultCommandsToQuerylessUrls)
        {
            FastPreconditions = Conditions.True; // TODO: this isn't quite right, I think there is a file extension condition somewhere?
        }
        else
        {
            //TODO optimize alloc
            FastPreconditions = Conditions.HasQueryStringKey(PathHelpers.SupportedQuerystringKeys.ToArray());
        }
    }
    public string Name => "CommandDefaults";
    public IFastCond? FastPreconditions { get; }

    public ValueTask<CodeResult<IRoutingEndpoint>?> ApplyRouting(MutableRequest request,
        CancellationToken cancellationToken = default)
    {
        if (FastPreconditions?.Matches(request) ?? false)
        {
            var query = request.MutableQueryString;
            foreach (var pair in options.CommandDefaults)
            {
                if (!query.ContainsKey(pair.Key))
                {
                    query[pair.Key] = pair.Value;
                }
            }
        }

        return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null);
    }
    
    /// ToString includes all data in the layer, for full diagnostic transparency, and lists the preconditions and data count
    public override string ToString()
    {
        return $"Routing Layer {Name}: {options.CommandDefaults.Count} defaults, Preconditions: {FastPreconditions}";
    }
    
    
    
}