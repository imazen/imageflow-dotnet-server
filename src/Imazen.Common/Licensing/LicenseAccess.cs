using System;

namespace Imazen.Common.Licensing
{
    /// <summary>
    /// Sharing of license keys.
    /// </summary>
    [Flags()]
    internal enum LicenseAccess
    {
        /// <summary>
        /// Only use licenses added to the instance.
        /// </summary>
        Local = 0,
        /// <summary>
        /// Reuse but don't share.
        /// </summary>
        ProcessReadonly = 1,
        /// <summary>
        /// Share but don't reuse
        /// </summary>
        ProcessShareOnly = 2,
        /// <summary>
        /// Share and reuse licenses process-wide
        /// </summary>
        Process = 3,
    }
    
}