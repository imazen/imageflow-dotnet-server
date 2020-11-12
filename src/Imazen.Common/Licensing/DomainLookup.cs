using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Imazen.Common.ExtensionMethods;
using Imazen.Common.Issues;

namespace Imazen.Common.Licensing
{
    internal class DomainLookup
    {
        /// <summary>
        ///     Limit the growth of the lookup table cache
        /// </summary>
        private const long LookupTableLimit = 8000;

        // For runtime use
        private readonly ConcurrentDictionary<string, string> lookupTable;

        /// <summary>
        ///     Used to locate a domain when it's not cached in lookupTable
        /// </summary>
        private readonly List<KeyValuePair<string, string>> suffixSearchList;

        // Track and limit runtime table lookup growth
        long lookupTableSize;


        private long LookupTableSize => lookupTableSize;

        public int KnownDomainCount => suffixSearchList.Count;

        public DomainLookup(ILicenseConfig c, IIssueReceiver sink, IEnumerable<ILicenseChain> licenseChains)
        {
            // What domains are mentioned in which licenses?
            var chainsByDomain = GetChainsByDomain(licenseChains);

            var knownDomains = chainsByDomain.Keys.ToList();

            // What custom mappings has the user set up?
            var customMappings = GetDomainMappings(c, sink, knownDomains);

            // Start with identity mappings and the mappings for the normalized domains.
            lookupTable = new ConcurrentDictionary<string, string>(
                customMappings.Concat(
                    knownDomains.Select(v => new KeyValuePair<string, string>(v, v)))
                , StringComparer.Ordinal);

            lookupTableSize = lookupTable.Count;

            // Set up a list for suffix searching
            suffixSearchList = knownDomains.Select(known =>
                                           {
                                               var d = known.TrimStart('.');
                                               d = d.StartsWith("www.") ? d.Substring(4) : d;
                                               return new KeyValuePair<string, string>("." + d, known);
                                           })
                                           .ToList();
        }

        Dictionary<string, IEnumerable<ILicenseChain>> GetChainsByDomain(IEnumerable<ILicenseChain> chains)
        {
            return chains.SelectMany(chain =>
                             chain.Licenses()
                                  .SelectMany(b => b.Fields.GetAllDomains())
                                  .Select(domain => new KeyValuePair<string, ILicenseChain>(domain, chain)))
                         .GroupBy(pair => pair.Key, pair => pair.Value,
                             (k, v) => new KeyValuePair<string, IEnumerable<ILicenseChain>>(k, v))
                         .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }


        Dictionary<string, string>
            GetDomainMappings(ILicenseConfig c, IIssueReceiver sink,
                              IReadOnlyCollection<string> knownDomains) //c.configurationSectionIssue
        {
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var pair in c.GetDomainMappings()) {
                var from = pair.Key;
                var to = pair.Value;
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) {
                    sink.AcceptIssue(new Issue($"Both from= and to= attributes are required on maphost, found {from} and {to}",
                        IssueSeverity.ConfigurationError));
                } else if (!from.EndsWith(".local") && from.IndexOf('.') > -1) {
                    sink.AcceptIssue(new Issue(
                        $"You can only map non-public hostnames to arbitrary licenses. Skipping {from}",
                        IssueSeverity.ConfigurationError));
                } else if (!knownDomains.Contains(to)) {
                    sink.AcceptIssue(new Issue(
                        $"You have mapped {from} to {to}. {to} is not one of the known domains: {string.Join(" ", knownDomains.OrderBy(s => s))}",
                        IssueSeverity.ConfigurationError));
                } else {
                    mappings[from] = to;
                }
            }
            return mappings;
        }

        public string ExplainNormalizations()
        {
            //Where(pair => pair.Value != null && pair.Key != pair.Value)
            return LookupTableSize > 0
                ? $"The domain lookup table has {LookupTableSize} elements. Displaying {Math.Min(200, LookupTableSize)}:\n" +
                   lookupTable.OrderByDescending(p => p.Value)
                                               .Take(200)
                                               .Select(pair => pair.Key == pair.Value ? pair.Key : $"{pair.Key} => {pair.Value}").Delimited(", ") +
                  "\n"
                : "";
        }

        /// <summary>
        ///     Returns null if there is no match or higher-level known domain.
        /// </summary>
        /// <param name="similarDomain"></param>
        /// <returns></returns>
        public string FindKnownDomain(string similarDomain)
        {
            // Bound ConcurrentDictionary growth; fail on new domains instead
            if (lookupTableSize <= LookupTableLimit) {
                return lookupTable.GetOrAdd(TrimLowerInvariant(similarDomain),
                    query =>
                    {
                        Interlocked.Increment(ref lookupTableSize);
                        return suffixSearchList.FirstOrDefault(
                                                   pair => query.EndsWith(pair.Key, StringComparison.Ordinal))
                                               .Value;
                    });
            }

            return lookupTable.TryGetValue(TrimLowerInvariant(similarDomain), out var result) ? result : null;
        }

        /// <summary>
        ///     Only cleans up string if require; otherwise an identity function
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public string TrimLowerInvariant(string s)
        {
            // Cleanup only if required
            return s.Any(c => char.IsUpper(c) || char.IsWhiteSpace(c)) ? s.Trim().ToLowerInvariant() : s;
        }
    }
}
