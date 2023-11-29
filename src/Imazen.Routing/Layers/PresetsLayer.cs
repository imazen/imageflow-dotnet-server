using System.Text;
using Imazen.Abstractions.Resulting;
using Imazen.Routing.Helpers;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Layers;

public enum PresetPriority
{
    DefaultValues = 0,
    OverrideQuery = 1
}
public class PresetOptions
{
    public string Name { get; }

    public PresetPriority Priority { get; }

    internal readonly Dictionary<string, string> Pairs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public PresetOptions(string name, PresetPriority priority, Dictionary<string, string> commands)
    {
        Name = name;
        Priority = priority;
        foreach (var pair in commands)
        {
            Pairs[pair.Key] = pair.Value;
        }
    }

    public PresetOptions SetCommand(string key, string value)
    {
        Pairs[key] = value;
        return this;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        var mode = Priority == PresetPriority.OverrideQuery ? "overrides" : "provides defaults";
        sb.Append($"Preset {Name} {mode} ");
        foreach (var pair in Pairs)
        {
            sb.Append($"{pair.Key}={pair.Value}, ");
        }

        return sb.ToString().TrimEnd(' ', ',');
    }
}

public record PresetsLayerOptions
{
    public bool UsePresetsExclusively { get; set; }
    public Dictionary<string, PresetOptions>? Presets { get; set; }
}

public class PresetsLayer(PresetsLayerOptions options) : IRoutingLayer
{
    public string Name => "Presets";
    public IFastCond? FastPreconditions { get; } = new Conditions.FastCondAny(new List<IFastCond>
    {
        Conditions.HasQueryStringKey("preset")
    });

    public ValueTask<CodeResult<IRoutingEndpoint>?> ApplyRouting(MutableRequest request, CancellationToken cancellationToken = default)
    {
        var query = request.MutableQueryString;
        if (options.UsePresetsExclusively)
        {
            var firstKey = query.FirstOrDefault().Key;
                
            if (query.Count > 1 || (firstKey != null && firstKey != "preset"))
            {
                return Tasks.ValueResult((CodeResult<IRoutingEndpoint>?)
                    CodeResult<IRoutingEndpoint>.Err((403, "Only presets are permitted in the querystring")));
            }
        }
        
        
        // Parse and apply presets before rewriting
        if (query.TryGetValue("preset", out var presetNames))
        {
            foreach (var presetName in presetNames)
            {
                if (presetName == null) continue;
                if (string.IsNullOrWhiteSpace(presetName)) continue;
                if (options.Presets?.TryGetValue(presetName, out var presetOptions) ?? false)
                {
                    foreach (var pair in presetOptions.Pairs)
                    {
                        if (presetOptions.Priority == PresetPriority.OverrideQuery ||
                            !query.ContainsKey(pair.Key))
                        {
                            query[pair.Key] = pair.Value;
                        }
                    }
                }
                else
                {
                    return new ValueTask<CodeResult<IRoutingEndpoint>?>(CodeResult<IRoutingEndpoint>.Err((400, 
                        $"The image preset {presetName} was referenced from the querystring but is not registered.")));
                }
            }
        }
        return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null);

    }
    
    /// ToString includes all data in the layer, for full diagnostic transparency, and lists the preconditions and data count
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Routing Layer {Name}: {options.Presets?.Count} presets, Preconditions: {FastPreconditions}");
        if (options.Presets == null) return sb.ToString();
        foreach (var pair in options.Presets)
        {
            sb.AppendLine($"  {pair.Value}");
        }
        return sb.ToString();
    }
}

