using Imazen.Routing.Layers;

namespace Imageflow.Server
{
    public enum SignatureRequired
    {
        ForAllRequests = 0,
        ForQuerystringRequests = 1,
        Never = 2
    }

  
    public class RequestSignatureOptions
    {
        internal SignatureRequired DefaultRequirement { get; }
        internal List<string> DefaultSigningKeys { get; }

        internal List<SignaturePrefix> Prefixes { get; } = new List<SignaturePrefix>();
        public bool IsEmpty => Prefixes.Count == 0 && DefaultSigningKeys.Count == 0 && DefaultRequirement == SignatureRequired.Never;
        public static RequestSignatureOptions Empty => new RequestSignatureOptions(SignatureRequired.Never, new List<string>());

        public RequestSignatureOptions(SignatureRequired defaultRequirement, IEnumerable<string> defaultSigningKeys)
        {
            DefaultRequirement = defaultRequirement;
            DefaultSigningKeys = defaultSigningKeys.ToList();
        }

        public RequestSignatureOptions ForPrefix(string prefix, StringComparison prefixComparison,
            SignatureRequired requirement, IEnumerable<string> signingKeys)
        {
            Prefixes.Add(new SignaturePrefix()
            {
                Prefix = prefix,
                PrefixComparison =  prefixComparison,
                Requirement =(Imazen.Routing.Layers.SignatureRequired) requirement,
                SigningKeys = signingKeys.ToList()
            });
            return this;
        }
    
        

    }
}