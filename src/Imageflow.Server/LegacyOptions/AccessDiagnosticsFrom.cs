namespace Imageflow.Server
{
    /// <summary>
    /// Where the diagnostics page can be accessed from
    /// </summary>
    public enum AccessDiagnosticsFrom
    {
        /// <summary>
        /// Do not allow unauthenticated access to the diagnostics page, even from localhost
        /// </summary>
        None,
        /// <summary>
        /// Only allow localhost to access the diagnostics page
        /// </summary>
        LocalHost,
        /// <summary>
        /// Allow any host to access the diagnostics page
        /// </summary>
        AnyHost
    }
}