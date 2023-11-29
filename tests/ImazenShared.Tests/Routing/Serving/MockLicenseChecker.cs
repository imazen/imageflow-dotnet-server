using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Layers;
using Imazen.Routing.Serving;

namespace Imazen.Tests.Routing.Serving;

public class MockLicenseChecker(Func<IHttpRequestStreamAdapter, bool> NeedsEnforcement, string LicenseMessage):ILicenseChecker
{
    public bool RequestNeedsEnforcementAction(IHttpRequestStreamAdapter request)
    {
        return NeedsEnforcement(request);
    }

    public string InvalidLicenseMessage => LicenseMessage;
    public string GetLicensePageContents()
    {
        return "";
    }

    public void FireHeartbeat()
    {
        
    }

    public void Initialize(LicenseOptions licenseOptions)
    {
        
    }

    public static MockLicenseChecker AlwaysOK()
    {
        return new MockLicenseChecker(request => false, "OK");
    }
}