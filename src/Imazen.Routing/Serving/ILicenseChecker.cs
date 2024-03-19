using Imageflow.Server;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Layers;

namespace Imazen.Routing.Serving;

public enum EnforceLicenseWith
{
    RedDotWatermark = 0,
    Http422Error = 1,
    Http402Error = 2
}
public interface ILicenseChecker
{
    EnforceLicenseWith? RequestNeedsEnforcementAction(IHttpRequestStreamAdapter request);
    
    string InvalidLicenseMessage { get; }
    string GetLicensePageContents();
    void FireHeartbeat();
    void Initialize(LicenseOptions licenseOptions);
}