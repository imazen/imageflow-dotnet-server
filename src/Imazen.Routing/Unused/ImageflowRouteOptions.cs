using System.Collections.Immutable;

namespace Imazen.Routing.Unused
{
        public record ImageflowRouteOptions
        {


                public IEnumerable<string>? UseSourceCaches { get; init; }
                public IEnumerable<string>? UseOutputCaches { get; init; }

                public string? CacheControlString { get; init; }

                
 
                //public SignatureRequired? RequestSignatureRequirement { get; } = null;
                public IImmutableList<string>? RequestSigningKeys { get; } = null;

                
                public bool LowercasePathRemainder { get; init; } = false;

     
                public bool OnlyAllowPresets { get; init; }

                public IImmutableDictionary<string, string>? ApplyDefaultCommands { get; init; } = null;

                // perhaps make an enum
                public bool ApplyDefaultCommandsToQuerylessUrls { get; init; } = false;

 
                // allows diag, licensing, heartbeat, etc
                public bool? AllowUtilityEndpoints { get; init; }
                
                // public IImmutableList<Func<UrlEventArgs, bool>>? PreRewriteAuthorization { get; init; } = null;
                // public IImmutableList<Func<UrlEventArgs, bool>>? Rewrite { get; init; } = null;
                // public IImmutableList<Func<UrlEventArgs, bool>>? PostRewriteAuthorization { get; init; } = null;
                // public IImmutableList<Action<WatermarkingEventArgs>>? AdjustWatermarking { get; init; } = null;
                //
                // public SecurityOptions? JobSecurityOptions { get; set; }
                //
                // internal readonly Dictionary<string, PresetOptions> Presets = new Dictionary<string, PresetOptions>(StringComparer.OrdinalIgnoreCase);

                
                // watermark definitions
                // license key enforcement
                // license key
                // DIAGNOSTICS ACCEESS
                // DIAGNOSTICS PASSWORD
        }
}