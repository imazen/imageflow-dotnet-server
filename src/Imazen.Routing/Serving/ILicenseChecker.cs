using Imageflow.Server;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Layers;

namespace Imazen.Routing.Serving;

public interface ILicenseChecker
{
    bool RequestNeedsEnforcementAction(IHttpRequestStreamAdapter request);
    
    string InvalidLicenseMessage { get; }
    string GetLicensePageContents();
    void FireHeartbeat();
    void Initialize(LicenseOptions licenseOptions);
}