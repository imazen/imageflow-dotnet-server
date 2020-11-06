using System;
using System.Collections.Generic;
using System.Linq;
using ImageResizer.Plugins.LicenseVerifier;
using Imazen.Common.Licensing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Imageflow.Server
{
    internal class Licensing : ILicenseConfig
    {
        private ImageflowMiddlewareOptions options;

        private Func<Uri> getCurrentRequestUrl;

        private LicenseManagerSingleton mgr;
        
        Computation cachedResult;
        internal Licensing(LicenseManagerSingleton mgr, Func<Uri> getCurrentRequestUrl = null)
        {
            this.mgr = mgr;
            this.getCurrentRequestUrl = getCurrentRequestUrl;
        }

        internal void Initialize(ImageflowMiddlewareOptions options)
        {
            this.options = options;
            mgr.MonitorLicenses(this);
            mgr.MonitorHeartbeat(this);

            // Ensure our cache is appropriately invalidated
            cachedResult = null;
            mgr.AddLicenseChangeHandler(this, (me, manager) => me.cachedResult = null);

            // And repopulated, so that errors show up.
            if (Result == null) {
                throw new ApplicationException("Failed to populate license result");
            }
        }

        public bool EnforcementEnabled()
        {
            return !string.IsNullOrEmpty(options.LicenseKey)
                || string.IsNullOrEmpty(options.MyOpenSourceProjectUrl);
        }
        public IEnumerable<KeyValuePair<string, string>> GetDomainMappings()
        {
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public IEnumerable<IEnumerable<string>> GetFeaturesUsed()
        {
            return new [] {new [] {"Imageflow"}};
        }

        public IEnumerable<string> GetLicenses()
        {
            if (!string.IsNullOrEmpty(options?.LicenseKey))
            {
                return Enumerable.Repeat(options.LicenseKey, 1);
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }

        public LicenseAccess LicenseScope => LicenseAccess.Local;

        public LicenseErrorAction LicenseError
        {
            get
            {
                switch (options.EnforcementMethod)
                {
                    case EnforceLicenseWith.RedDotWatermark:
                        return LicenseErrorAction.Watermark;
                    case EnforceLicenseWith.Http422Error:
                        return LicenseErrorAction.Http422;
                    case EnforceLicenseWith.Http402Error:
                        return LicenseErrorAction.Http402;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public event LicenseConfigEvent LicensingChange;
        
        public event LicenseConfigEvent Heartbeat;
        public bool IsImageflow => true;
        public bool IsImageResizer => false;
        public string LicensePurchaseUrl => "https://imageresizing.net/licenses";
        
        Computation Result
        {
            get {
                if (cachedResult?.ComputationExpires != null &&
                    cachedResult.ComputationExpires.Value < mgr.Clock.GetUtcNow()) {
                    cachedResult = null;
                }
                return cachedResult ??= new Computation(this, mgr.TrustedKeys,mgr, mgr,
                    mgr.Clock, EnforcementEnabled());
            }
        }

        public string InvalidLicenseMessage =>
            "Imageflow cannot validate your license; visit /imageflow.debug or /imageflow.license to troubleshoot.";

        internal bool RequestNeedsEnforcementAction(HttpRequest request)
        {
            if (!EnforcementEnabled()) {
                return false;
            }
            
            var requestUrl = getCurrentRequestUrl != null ? getCurrentRequestUrl() : 
                new Uri(request.GetEncodedUrl());

            var isLicensed = Result.LicensedForRequestUrl(requestUrl);
            if (isLicensed) {
                return false;
            }

            if (requestUrl == null && Result.LicensedForSomething()) {
                return false;
            }

            return true;
        }


        public string GetLicensePageContents()
        {
            return Result.ProvidePublicText();
        }
    }
}