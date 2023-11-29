using Imazen.Common.Licensing;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Serving;

namespace Imazen.Routing.Layers;

public class LicenseOptions{
    internal string? LicenseKey { get; set; } = "";
    internal string? MyOpenSourceProjectUrl { get; set; } = "";
        
    internal string KeyPrefix { get; set; } = "imageflow_";
    public required string[] CandidateCacheFolders { get; set; }
    internal EnforceLicenseWith EnforcementMethod { get; set; } = EnforceLicenseWith.RedDotWatermark;
        
}
internal class Licensing : ILicenseConfig, ILicenseChecker, IHasDiagnosticPageSection
{
        
    private readonly Func<Uri?>? getCurrentRequestUrl;

    private readonly LicenseManagerSingleton mgr;

    private Computation? cachedResult;
    internal Licensing(LicenseManagerSingleton mgr, Func<Uri?>? getCurrentRequestUrl = null)
    {
        this.mgr = mgr;
        this.getCurrentRequestUrl = getCurrentRequestUrl;
    }
    private LicenseOptions? options;
    public void Initialize(LicenseOptions licenseOptions)
    {
        options = licenseOptions;
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

    private bool EnforcementEnabled()
    {
        return options != null && (!string.IsNullOrEmpty(options.LicenseKey)
                                   || string.IsNullOrEmpty(options.MyOpenSourceProjectUrl));
    }
    public IEnumerable<KeyValuePair<string, string>> GetDomainMappings()
    {
        return Enumerable.Empty<KeyValuePair<string, string>>();
    }

    public IReadOnlyCollection<IReadOnlyCollection<string>> GetFeaturesUsed()
    {
        return new [] {new [] {"Imageflow"}};
    }

    public IEnumerable<string> GetLicenses()
    {
        return !string.IsNullOrEmpty(options?.LicenseKey) ? Enumerable.Repeat(options!.LicenseKey, 1) : Enumerable.Empty<string>();
    }

    public LicenseAccess LicenseScope => LicenseAccess.Local;

    public LicenseErrorAction LicenseEnforcement
    {
        get
        {
            if (options == null) {
                return LicenseErrorAction.Http422;
            }
            return options.EnforcementMethod switch
            {
                EnforceLicenseWith.RedDotWatermark => LicenseErrorAction.Watermark,
                EnforceLicenseWith.Http422Error => LicenseErrorAction.Http422,
                EnforceLicenseWith.Http402Error => LicenseErrorAction.Http402,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public string EnforcementMethodMessage
    {
        get
        {
            return LicenseEnforcement switch
            {
                LicenseErrorAction.Watermark =>
                    "You are using EnforceLicenseWith.RedDotWatermark. If there is a licensing error, an red dot will be drawn on the bottom-right corner of each image. This can be set to EnforceLicenseWith.Http402Error instead (valuable if you are externally caching or storing result images.)",
                LicenseErrorAction.Http422 =>
                    "You are using EnforceLicenseWith.Http422Error. If there is a licensing error, HTTP status code 422 will be returned instead of serving the image. This can also be set to EnforceLicenseWith.RedDotWatermark.",
                LicenseErrorAction.Http402 =>
                    "You are using EnforceLicenseWith.Http402Error. If there is a licensing error, HTTP status code 402 will be returned instead of serving the image. This can also be set to EnforceLicenseWith.RedDotWatermark.",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

#pragma warning disable CS0067
    public event LicenseConfigEvent? LicensingChange;
#pragma warning restore CS0067
        
    public event LicenseConfigEvent? Heartbeat;
    public bool IsImageflow => true;
    public bool IsImageResizer => false;
    public string LicensePurchaseUrl => "https://imageresizing.net/licenses";

    public string AgplCompliantMessage
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(options?.MyOpenSourceProjectUrl))
            {
                return "You have certified that you are complying with the AGPLv3 and have open-sourced your project at the following url:\r\n"
                       + options!.MyOpenSourceProjectUrl;
            }
            else
            {
                return "";
            }
        }
    }

    internal Computation Result
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

    public bool RequestNeedsEnforcementAction(IHttpRequestStreamAdapter request)
    {
        if (!EnforcementEnabled()) {
            return false;
        }
            
        var requestUrl = getCurrentRequestUrl != null ? getCurrentRequestUrl() : 
            request.GetUri();

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
        return Result.ProvidePublicLicensesPage();
    }

    public void FireHeartbeat()
    {
        Heartbeat?.Invoke(this, this);
    }

    public string? GetDiagnosticsPageSection(DiagnosticsPageArea section)
    {
        if (section != DiagnosticsPageArea.End)
        {
            return Result.ProvidePublicLicensesPage();
        }
        var s = new System.Text.StringBuilder();
        s.AppendLine(
            "\n\nWhen fetching a remote license file (if you have one), the following information is sent via the querystring.");
        foreach (var pair in Result.GetReportPairs().GetInfo())
        {
            s.AppendFormat("   {0,32} {1}\n", pair.Key, pair.Value);
        }
            
            
        s.AppendLine(Result.DisplayLastFetchUrl());
        return s.ToString();
    }
}