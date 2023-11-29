using System.Text;
using Imazen.Abstractions.HttpStrings;
using Imazen.Abstractions.Resulting;
using Imazen.Common.Helpers;
using Imazen.Routing.Helpers;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Layers;

public enum SignatureRequired
{
    ForAllRequests = 0,
    ForQuerystringRequests = 1,
    Never = 2
}

internal struct SignaturePrefix
{
    internal string Prefix { get; set; }
    internal StringComparison PrefixComparison { get; set; }
    internal SignatureRequired Requirement { get; set; }
    internal List<string> SigningKeys { get; set; }
    
    override public string ToString()
    {
        return $"{Prefix}{PrefixComparison.ToStringShort()} -> {Requirement} ({SigningKeys.Count} keys defined)";
    }
}

public class RequestSignatureOptions
{
    internal SignatureRequired DefaultRequirement { get; }
        
    internal List<string> DefaultSigningKeys { get; }

    internal List<SignaturePrefix> Prefixes { get; } = new List<SignaturePrefix>();
        
    public bool IsEmpty => Prefixes.Count == 0 && DefaultSigningKeys.Count == 0 && DefaultRequirement == SignatureRequired.Never;

    internal RequestSignatureOptions(SignatureRequired defaultRequirement, IEnumerable<string> defaultSigningKeys)
    {
        DefaultRequirement = defaultRequirement;
        DefaultSigningKeys = defaultSigningKeys.ToList();
    }
        
    protected void AddPrefix(string prefix, StringComparison prefixComparison, SignatureRequired requirement, IEnumerable<string> signingKeys)
    {
        Prefixes.Add(new SignaturePrefix()
        {
            Prefix = prefix,
            PrefixComparison =  prefixComparison,
            Requirement = requirement,
            SigningKeys = signingKeys.ToList()
        });
    }
    internal RequestSignatureOptions AddAllPrefixes(IEnumerable<SignaturePrefix> prefixes)
    {
        Prefixes.AddRange(prefixes);
        return this;
    }
    internal Tuple<SignatureRequired, ICollection<string>> GetRequirementForPath(string path)
    {
        if (path != null)
        {
            foreach (var p in Prefixes)
            {
                if (path.StartsWith(p.Prefix, p.PrefixComparison))
                {
                    return new Tuple<SignatureRequired, ICollection<string>>(p.Requirement, p.SigningKeys);
                }
            }
        }
        return new Tuple<SignatureRequired, ICollection<string>>(DefaultRequirement, DefaultSigningKeys);
    }
    /// ToString includes all data in the layer, for full diagnostic transparency, and lists the preconditions and data count
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Default Signing Requirement: {DefaultRequirement}, Defined Signing Keys: {DefaultSigningKeys.Count}");
        foreach (var prefix in Prefixes)
        {
            sb.AppendLine($"  {prefix}");
        }
        return sb.ToString();
    }

}
public record struct SignatureVerificationLayerOptions(RequestSignatureOptions RequestSignatureOptions);

public class SignatureVerificationLayer : IRoutingLayer
{
    private readonly SignatureVerificationLayerOptions options;
    public SignatureVerificationLayer(SignatureVerificationLayerOptions options)
    {
        this.options = options;
        if (options.RequestSignatureOptions.IsEmpty)
        {
            FastPreconditions = null;
        }
        else
        {
            // We probably need to filter by extension tho, and prefixes listed
            FastPreconditions = Conditions.True;
        }
    }
    
    public string Name => "SignatureVerification";
    public IFastCond? FastPreconditions { get; }
    
    private bool VerifySignature(MutableRequest request, ref string authorizedMessage)
    {
        var (requirement, signingKeys) = options.RequestSignatureOptions
            .GetRequirementForPath(request.Path);
        
        if (request.MutableQueryString.TryGetValue("signature", out var actualSignature))
        {
            if (request.IsChildRequest)
            {
                // We can't allow or verify child requests.
                authorizedMessage = "Child job requests (such as for watermarks) need not and cannot be signed.";
                return false;
            }
            var queryString = UrlQueryString.Create(request.MutableQueryString).ToString();

            var pathBase = request.OriginatingRequest?.GetPathBase() ?? UrlPathString.Empty;
            
            var pathAndQuery =  pathBase.HasValue
                ? "/" + pathBase.Value.TrimStart('/')
                : "";
            pathAndQuery += request.Path + queryString;

            pathAndQuery = Signatures.NormalizePathAndQueryForSigning(pathAndQuery);
            
            var actualSignatureString = actualSignature.ToString();
            foreach (var key in signingKeys)
            {
                var expectedSignature = Signatures.SignString(pathAndQuery, key, 16);
                // ordinal comparison
                if (string.Equals(expectedSignature, actualSignatureString, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            authorizedMessage = "Image signature does not match request, or used an invalid signing key.";
            return false;

        }

        if (requirement == SignatureRequired.Never || request.IsChildRequest)
        {
            return true;
        }
        if (requirement == SignatureRequired.ForQuerystringRequests)
        {
            if (request.MutableQueryString.Count == 0) return true;
            
            authorizedMessage = "Image processing requests must be signed. No &signature query key found. ";
            return false;
        }
        authorizedMessage = "Image requests must be signed. No &signature query key found. ";
        return false;

    }

    public ValueTask<CodeResult<IRoutingEndpoint>?> ApplyRouting(MutableRequest request,
        CancellationToken cancellationToken = default)
    {
        if (FastPreconditions == null || !FastPreconditions.Matches(request)) return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null);
        
        var authorizedMessage = "";
        if (VerifySignature(request, ref authorizedMessage))
        {
            return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null);
        }

        return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(
            CodeResult<IRoutingEndpoint>.Err((403, authorizedMessage)));
    }
    
    /// ToString includes all data in the layer, for full diagnostic transparency, and lists the preconditions and data count
    public override string ToString()
    {
        return $"Routing Layer {Name} Preconditions: {FastPreconditions}\n{options.RequestSignatureOptions}\n";
    }
}