using Imazen.Abstractions.Resulting;
using Imazen.Routing.Promises.Pipelines.Watermarking;
using Imazen.Routing.Helpers;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Requests;
using Imazen.Routing.Serving;

namespace Imazen.Routing.Layers;


internal class LicensingLayer(ILicenseChecker licenseChecker) : IRoutingLayer
{
    public string Name => "Licensing";
    public IFastCond? FastPreconditions => null;
    public ValueTask<CodeResult<IRoutingEndpoint>?> ApplyRouting(MutableRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OriginatingRequest == null) return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null);
        var licenseAction = licenseChecker.RequestNeedsEnforcementAction(request.OriginatingRequest);
      
        if (licenseAction is EnforceLicenseWith.Http402Error or EnforceLicenseWith.Http422Error)
        {
            var response = SmallHttpResponse.NoStoreNoRobots(new HttpStatus(
                licenseAction == EnforceLicenseWith.Http402Error ? 402 : 422
                , licenseChecker.InvalidLicenseMessage));
            return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(new PredefinedResponseEndpoint(response));
        }

        if (licenseAction == EnforceLicenseWith.RedDotWatermark)
        {
            request.MutableQueryString["watermark_red_dot"] = "true";
        }

        return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null);
    }
    
    /// ToString includes all data in the layer, for full diagnostic transparency, and lists the preconditions and data count
    public override string ToString()
    {
        return $"Routing Layer {Name}";
    }
}