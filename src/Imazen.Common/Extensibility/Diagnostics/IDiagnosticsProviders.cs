namespace Imazen.Common.Extensibility.Diagnostics
{
    internal interface IDiagnosticsProvider
    {
        string ProvideDiagnostics();
    }
    internal interface IDiagnosticsHeaderProvider
    {
        string ProvideDiagnosticsHeader();
    }
    internal interface IDiagnosticsFooterProvider
    {
        string ProvideDiagnosticsFooter();
    }

    internal interface ILicenseDiagnosticsProvider
    {
        string ProvidePublicText();
    }
}