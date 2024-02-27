﻿// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the GNU Affero General Public License, Version 3.0.
// Commercial licenses available at http://imageresizing.net/

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Imazen.Common.ExtensionMethods;
using Imazen.Common.Instrumentation;
using Imazen.Common.Instrumentation.Support.InfoAccumulators;
using Imazen.Common.Issues;

namespace Imazen.Common.Licensing
{
    /// <summary>
    ///     Computes an (expiring) boolean result for whether the software is licensed for the functionality installed on the
    ///     Config, and the license data instantly available
    ///     Transient issues are stored within the class; permanent issues are stored in the  provided sink
    /// </summary>
    internal class Computation : IssueSink
    {

        
        /// <summary>
        ///     If a placeholder license doesn't specify NetworkGraceMinutes, we use this value.
        /// </summary>
        const int DefaultNetworkGraceMinutes = 6;

        const int UnknownDomainsLimit = 200;

        readonly IList<ILicenseChain> chains;
        readonly ILicenseClock clock;
        readonly DomainLookup domainLookup;

        readonly ILicenseManager mgr;
        readonly IIssueReceiver permanentIssues;

        // This is mutated to track unknown domains
        readonly ConcurrentDictionary<string, bool> unknownDomains = new ConcurrentDictionary<string, bool>();

        bool EverythingDenied { get; }
        bool AllDomainsLicensed { get; }
        bool EnforcementEnabled { get; }
        IDictionary<string, bool> KnownDomainStatus { get; }
        public DateTimeOffset? ComputationExpires { get; }
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        LicenseAccess Scope { get; }
        // ReSharper disable once ArrangeTypeMemberModifiers
        LicenseErrorAction LicenseError { get; }

        private ILicenseConfig LicenseConfig { get; }
        public Computation(ILicenseConfig c, IReadOnlyCollection<RSADecryptPublic> trustedKeys,
                           IIssueReceiver permanentIssueSink,
                           ILicenseManager mgr, ILicenseClock clock, bool enforcementEnabled) : base("Computation")
        {
            permanentIssues = permanentIssueSink;
            EnforcementEnabled = enforcementEnabled;
            this.clock = clock;
            LicenseConfig = c;
            Scope = c.LicenseScope;
            LicenseError = c.LicenseEnforcement;
            this.mgr = mgr;
            if (mgr.FirstHeartbeat == null) {
                throw new ArgumentException("ILicenseManager.Heartbeat() must be called before Computation.new");
            }

            // What features are installed on this instance?
            // For a license to be OK, it must have one of each of this nested list;
            IReadOnlyCollection<IReadOnlyCollection<string>> pluginFeaturesUsed = c.GetFeaturesUsed();

            // Create or fetch all relevant license chains; ignore the empty/invalid ones, they're logged to the manager instance
            chains = c.GetLicenses()
                      .Select(str => mgr.GetOrAdd(str, c.LicenseScope))
                      .Where(x => x != null && x.Licenses().Any())
                      .Concat(c.LicenseScope.HasFlag(LicenseAccess.ProcessReadonly)
                          ? mgr.GetSharedLicenses()
                          : Enumerable.Empty<ILicenseChain>())
                      .Distinct()
                      .Cast<ILicenseChain>()
                      .ToList();


            // Set up our domain map/normalize/search manager
            domainLookup = new DomainLookup(c, permanentIssueSink, chains);


            // Check for tampering via interfaces
            if (chains.Any(chain => chain.Licenses().Any(b => !b.Revalidate(trustedKeys)))) {
                EverythingDenied = true;
                permanentIssueSink.AcceptIssue(new Issue(
                    "Licenses failed to revalidate; please contact support@imazen.io", IssueSeverity.Error));
            }

            // Look for grace periods
            var gracePeriods = chains.Where(IsPendingLicense).Select(GetGracePeriodFor).ToList();

            // Look for fetched and valid licenses
            var validLicenses = chains.Where(chain => !IsPendingLicense(chain))
                                      .SelectMany(chain => chain.Licenses())
                                      .Where(b => !b.Fields.IsRemotePlaceholder() && IsLicenseValid(b))
                                      .ToList();

            // This computation expires when we cross an expires, issued date, or NetworkGracePeriod expiration
            ComputationExpires = chains.SelectMany(chain => chain.Licenses())
                                       .SelectMany(b => new[] {b.Fields.Expires, b.Fields.ImageflowExpires, b.Fields.Issued})
                                       .Concat(gracePeriods)
                                       .Where(date => date != null)
                                       .OrderBy(d => d)
                                       .FirstOrDefault(d => d > clock.GetUtcNow());

            AllDomainsLicensed = gracePeriods.Any(t => t != null) ||
                                 validLicenses
                                     .Any(license => !license.Fields.GetAllDomains().Any() && AreFeaturesLicensed(license, pluginFeaturesUsed, false));

            KnownDomainStatus = validLicenses.SelectMany(
                                                 b => b.Fields.GetAllDomains()
                                                       .SelectMany(domain => b.Fields.GetFeatures()
                                                                              .Select(
                                                                                  feature => new
                                                                                      KeyValuePair<string, string>(
                                                                                          domain, feature))))
                                             .GroupBy(pair => pair.Key, pair => pair.Value,
                                                 (k, v) => new KeyValuePair<string, IEnumerable<string>>(k, v))
                                             .Select(pair => new KeyValuePair<string, bool>(pair.Key,
                                                 pluginFeaturesUsed.All(
                                                     set => set.Intersect(pair.Value, StringComparer.OrdinalIgnoreCase)
                                                               .Any())))
                                             .ToDictionary(pair => pair.Key, pair => pair.Value,
                                                 StringComparer.Ordinal);

            if (UpgradeNeeded()) {
                foreach (var b in validLicenses)
                    AreFeaturesLicensed(b, pluginFeaturesUsed, true);
            }
        }


        private bool IsLicenseExpired(ILicenseDetails details)
        {
            if (LicenseConfig.IsImageflow)
            {
                return details.ImageflowExpires != null &&
                       details.ImageflowExpires < clock.GetUtcNow();
            }

            return details.Expires != null &&
                   details.Expires < clock.GetUtcNow();
        }

        private bool HasLicenseBegun(ILicenseDetails details) => details.Issued != null &&
                                                         details.Issued < clock.GetUtcNow();


        public IEnumerable<string> GetMessages(ILicenseDetails d) => new[] {
            d.GetMessage(),
            IsLicenseExpired(d) ? d.GetExpiryMessage() : null,
            d.GetRestrictions()
        }.Where(s => !string.IsNullOrWhiteSpace(s)).Cast<string>();


    

        public DateTimeOffset? GetBuildDate() => clock.GetBuildDate() ?? clock.GetAssemblyWriteDate();

        private bool IsBuildDateNewer(DateTimeOffset? value)
        {
            var buildDate = GetBuildDate();
            return buildDate != null &&
                   value != null &&
                   buildDate > value;
        }

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        private bool AreFeaturesLicensed(ILicenseBlob b, IEnumerable<IEnumerable<string>> oneFromEach, bool logIssues)
        {
            var licenseFeatures = b.Fields.GetFeatures();
            var notCovered = oneFromEach.Where(
                set => !set.Intersect(licenseFeatures, StringComparer.OrdinalIgnoreCase).Any());
            var success = !notCovered.Any();
            if (!success && logIssues) {
                permanentIssues.AcceptIssue(new Issue(
                    $"License {b.Fields.Id} needs to be upgraded; it does not cover in-use features {notCovered.SelectMany(v => v).Distinct().Delimited(", ")}", b.ToRedactedString(),
                    IssueSeverity.Error));
            }
            return success;
        }
        

        private bool IsLicenseValid(ILicenseBlob b)
        {
            var details = b.Fields;
            if (IsLicenseExpired(details)) {
                permanentIssues.AcceptIssue(new Issue("License " + details.Id + " has expired.", b.ToRedactedString(),
                    IssueSeverity.Error));
                return false;
            }

            if (!HasLicenseBegun(details)) {
                permanentIssues.AcceptIssue(new Issue(
                    "License " + details.Id + " was issued in the future; check system clock.", b.ToRedactedString(),
                    IssueSeverity.Error));
                return false;
            }

            if (IsBuildDateNewer(details.SubscriptionExpirationDate)) {
                permanentIssues.AcceptIssue(new Issue(
                    $"License {details.Id} covers product versions prior to {details.SubscriptionExpirationDate?.ToString("D")}, but you are using a build dated {GetBuildDate()?.ToString("D")}",
                    b.ToRedactedString(),
                    IssueSeverity.Error));
                return false;
            }
            if (details.IsRevoked()) {
                var message = b.Fields.GetMessage();
                permanentIssues.AcceptIssue(new Issue($"License {details.Id}" + (message != null ? $": {message}" : " is no longer valid"),
                    b.ToRedactedString(), IssueSeverity.Error));
                return false;
            }
            return true;
        }

        private bool IsPendingLicense(ILicenseChain chain)
        {
            return chain.IsRemote && chain.Licenses().All(b => b.Fields.IsRemotePlaceholder());
        }

        /// <summary>
        ///     Pending licenses can offer grace periods. Logs a local issue; trusts the instance (and issue) will be cleared
        ///     when the returned DateTime passes. May subdivide a grace period for more granular issue text.
        /// </summary>
        /// <param name="chain"></param>
        /// <returns></returns>
        private DateTimeOffset? GetGracePeriodFor(ILicenseChain chain)
        {
            // If the placeholder license fails its own constraints, don't add a grace period
            if (chain.Licenses().All(b => !IsLicenseValid(b))) {
                return null;
            }

            var graceMinutes = chain.Licenses()
                                    .Where(IsLicenseValid)
                                    .Select(b => b.Fields.NetworkGraceMinutes())
                                    .OrderByDescending(v => v)
                                    .FirstOrDefault() ?? DefaultNetworkGraceMinutes;

            // Success will automatically replace this instance. Warn immediately.
            Debug.Assert(mgr.FirstHeartbeat != null, "mgr.FirstHeartbeat != null");
            var firstHeartbeat = mgr.FirstHeartbeat ?? clock.GetUtcNow();
            // NetworkGraceMinutes Expired?
            var expires = firstHeartbeat.AddMinutes(graceMinutes);
            if (expires < clock.GetUtcNow()) {
                permanentIssues.AcceptIssue(new Issue($"Grace period of {graceMinutes}m expired for license {chain.Id}",
                    $"License {chain.Id} was not found in the disk cache and could not be retrieved from the remote server within {graceMinutes} minutes.",
                    IssueSeverity.Error));
                return null;
            }

            // Less than 30 seconds since boot time?
            var thirtySeconds = firstHeartbeat.AddSeconds(30);
            if (thirtySeconds > clock.GetUtcNow()) {
                AcceptIssue(new Issue($"Fetching license {chain.Id} (not found in disk cache).",
                    $"Network grace period expires in {graceMinutes} minutes", IssueSeverity.Warning));
                return thirtySeconds;
            }

            // Otherwise in grace period
            AcceptIssue(new Issue(
                $"Grace period of {graceMinutes}m will expire for license {chain.Id} at UTC {expires:HH:mm} on {expires:D}",
                $"License {chain.Id} was not found in the disk cache and could not be retrieved from the remote server.",
                IssueSeverity.Error));

            return expires;
        }


        public bool LicensedForAll() => !EverythingDenied && AllDomainsLicensed;

        public bool LicensedForSomething()
        {
            return !EverythingDenied &&
                   (AllDomainsLicensed || (domainLookup.KnownDomainCount > 0));
        }

        public bool NoLicenseInstalled => !chains.Any();

        public bool NoLicenseRequired => !LicenseConfig.GetFeaturesUsed().Any();
        
        public bool LicensePresentButInvalid =>
            !NoLicenseRequired && !EverythingDenied && !AllDomainsLicensed
            && !KnownDomainStatus.Any() && !NoLicenseInstalled;


        public bool UpgradeNeeded() =>  !AllDomainsLicensed || KnownDomainStatus.Values.Contains(false);

        public string? ManageSubscriptionUrl => chains
            .SelectMany(c => c.Licenses())
            .Select(l => l.Fields.Get("ManageYourSubscription"))
            .FirstOrDefault(v => !string.IsNullOrEmpty(v));
        
        public bool LicensedForRequestUrl(Uri? url)
        {
            if (EverythingDenied) {
                return false;
            }
            if (AllDomainsLicensed) {
                return true;
            }
            var host = url?.DnsSafeHost;
            if (domainLookup.KnownDomainCount > 0 && host != null) {
                var knownDomain = domainLookup.FindKnownDomain(host);
                if (knownDomain != null) {
                    return KnownDomainStatus[knownDomain];
                }
                if (unknownDomains.Count < UnknownDomainsLimit) {
                    unknownDomains.TryAdd(domainLookup.TrimLowerInvariant(host), false);
                }
                return false;
            }
            return false;
        }


        

        public string LicenseStatusSummary()
        {
            if (NoLicenseRequired)
            {
                return "License key not required.";
            }
            if (EverythingDenied) {
                return "License key error. Contact support@imazen.io";
            }
            if (AllDomainsLicensed) {
                return "License key valid for all domains.";
            }
            if (KnownDomainStatus.Any())
            {
                
                var valid = KnownDomainStatus.Where(pair => pair.Value).Select(pair => pair.Key).ToArray();
                var invalid = KnownDomainStatus.Where(pair => !pair.Value).Select(pair => pair.Key).ToArray();
                var notCovered = unknownDomains.Select(pair => pair.Key).ToArray();

                var sb = new StringBuilder($"License valid for {valid.Length} domains");
                if (invalid.Length > 0)
                    sb.Append($", insufficient for {invalid.Length} domains");
                if (notCovered.Length > 0)
                    sb.Append($", missing for {notCovered.Length} domains");

                sb.Append(": ");

                sb.Append(valid.Select(s => $"{s} (valid)")
                               .Concat(invalid.Select(s => $"{s} (not sufficient)"))
                               .Concat(notCovered.Select(s => $"{s} (not licensed)"))
                               .Delimited(", "));
                return sb.ToString();
            }
   
            return chains.Any() ? "No valid license keys found." : "No license keys found.";
        }

        IEnumerable<string> GetMessages() =>
            chains.SelectMany(c => c.Licenses()
                                    .SelectMany(l => GetMessages(l.Fields))
                                    .Select(s => $"License {c.Id}: {s}"));

        string RestrictionsAndMessages() => GetMessages().Delimited("\r\n");


        internal bool IsUsingPermanentDomainKeys => mgr.GetAllLicenses().All(l => !l.IsRemote && l.Id.Contains("."));

        string GetPublicLicenseHeader()
        {
            if (NoLicenseRequired)
            {
                return "No license key required; only free features in use.\r\n";
            }
            
            var sb = new StringBuilder();
            var hr = EnforcementEnabled
                ? "---------------------- License Validation ON ----------------------\r\n"
                : "---------------------- License Validation OFF -----------------------\r\n";

            sb.Append(hr);
            if (!EnforcementEnabled)
            {
                sb.Append(LicenseConfig.AgplCompliantMessage);
                if (!string.IsNullOrEmpty(LicenseConfig.AgplCompliantMessage))
                {
                    sb.Append("\r\n\r\n");
                }
                if (LicenseConfig.IsImageResizer)
                {
                    sb.Append("You are using a DRM-disabled version of ImageResizer. License enforcement is OFF.\r\n");
                    sb.Append("Please see if your purchase is eligible for a free key: https://imageresizing.net/licenses/convert.\r\n");
                    sb.Append("NuGet sometimes caches DRM versions instead of the DRM-free, so please use a key if possible.\r\n");
                    
                }

                if (LicenseConfig.IsImageflow)
                {
                    sb.Append(
                        "You have chosen to abide by the AGPLv3, which can be found at https://www.gnu.org/licenses/agpl-3.0.en.html\r\n\r\n");
                    sb.Append("Please ensure that the URL to your open source project is correctly registered via ImageflowMiddlewareOptions.SetMyOpenSourceProjectUrl().\r\n");
                    
                }
            }
            sb.Append("\r\n");
            sb.Append(LicenseStatusSummary());
            sb.Append("\r\n\r\n");
            
            if (LicenseConfig.IsImageResizer && 
                IsUsingPermanentDomainKeys) {
                sb.Append("Need to change domains? Get a discounted upgrade to a floating license: https://imageresizing.net/licenses/convert\r\n\r\n");
            }

            if (EnforcementEnabled)
            {
                if (NoLicenseInstalled)
                {
                    sb.Append(
                        $"!!! You must purchase a license key or comply with the AGPLv3.\r\nTo get a license key, visit {LicenseConfig.LicensePurchaseUrl}\r\n\r\n");
                }else if (LicensePresentButInvalid)
                {
                    // Missing feature codes (could be edition OR version, i.e, R4Performance vs R_Performance
                    sb.Append(
                        $"!!! Your license is invalid. Please renew your license via the management portal or purchase a new one at {LicenseConfig.LicensePurchaseUrl}\r\n\r\n");

                }
                else if (UpgradeNeeded())
                {
                    // Missing feature codes (could be edition OR version, i.e, R4Performance vs R_Performance
                    sb.Append(
                        $"!!! Your license needs to be upgraded. To upgrade your license, visit {LicenseConfig.LicensePurchaseUrl}\r\n\r\n");
                }
            }

            var messages = RestrictionsAndMessages();

            if (!string.IsNullOrEmpty(messages))
            {
                sb.Append("!!!!!!!!!!!!!!!!!!!!!!!!\r\n");
                sb.Append(messages);
                sb.Append("\r\n!!!!!!!!!!!!!!!!!!!!!!!!\r\n\r\n");
            }
            sb.Append(LicenseConfig.EnforcementMethodMessage);
            sb.Append("\r\n");

            if (!string.IsNullOrEmpty(ManageSubscriptionUrl))
            {
                sb.Append($"Manage your subscription at {ManageSubscriptionUrl}\r\n");
            }

            sb.Append(
                "For help with your license, email support@imazen.io and include the contents of this page.\r\n");
            sb.Append(hr);
            sb.Append("\r\n");
            return sb.ToString();
        }


        public string ProvideDiagnostics() => ListLicensesInternal(c => c.ToString() ?? "(null)");

        public string ProvidePublicLicensesPage() => GetPublicLicenseHeader() + ListLicensesInternal(c => c.ToPublicString());

        string ListLicensesInternal(Func<ILicenseChain, string> stringifyChain)
        {
            var sb = new StringBuilder();
            if (chains.Count > 0)
            {
                sb.AppendLine("Licenses for this instance:\n");
                sb.AppendLine(string.Join("\n", chains.Select(stringifyChain)));
            }
            var others = mgr.GetAllLicenses().Except(chains).Select(stringifyChain).ToList();
            if (others.Any())
            {
                sb.AppendLine("Licenses only used by other instances in this process:\n");
                sb.AppendLine(string.Join("\n", others));
            }
            sb.AppendLine();
            sb.Append(domainLookup.ExplainNormalizations());
            return sb.ToString();
        }



        public string DisplayLastFetchUrl()
        {
            return "The most recent license fetch used the following URL:\r\n\r\n" +
                   mgr.GetAllLicenses().Select(c => c.LastFetchUrl()).Delimited("\r\n");
        }

        internal IInfoAccumulator GetReportPairs() => (mgr as LicenseManagerSingleton)?.GetReportPairs() ??
                                             GlobalPerf.Singleton.GetReportPairs();

    }
}
