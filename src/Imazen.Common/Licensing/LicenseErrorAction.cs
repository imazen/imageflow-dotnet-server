namespace Imazen.Common.Licensing
{
    /// <summary>
    /// How to notify the user that license validation has failed
    /// </summary>
    [Flags]
    internal enum LicenseErrorAction
    {
        /// <summary>
        /// Adds a red dot to the bottom-right corner
        /// </summary>
        Watermark = 0,
        /// <summary>
        /// Returns http status code 402
        /// </summary>
        Http402 = 1,
        /// <summary>
        /// Returns HTTP status code 422
        /// </summary>
        Http422 = 2,
    }
}