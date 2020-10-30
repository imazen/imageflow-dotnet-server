using System;
using System.Collections.Generic;
using System.Linq;

namespace Imageflow.Server
{
    public enum SignatureRequired
    {
        ForAllRequests,
        ForQuerystringRequests,
        Never
    }

    internal struct SignaturePrefix
    {
        internal string Prefix { get; set; }
        internal StringComparison PrefixComparison { get; set; }
        internal SignatureRequired Requirement { get; set; }
        internal List<string> SigningKeys { get; set; }


    }
    public class RequestSignatureOptions
    {
        internal SignatureRequired DefaultRequirement { get; }
        internal List<string> DefaultSigningKeys { get; }

        internal List<SignaturePrefix> Prefixes { get; } = new List<SignaturePrefix>();
        

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
                Requirement = requirement,
                SigningKeys = signingKeys.ToList()
            });
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

    }
}