using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imazen.Common.Persistence
{
    /// <summary>
    /// The result of the cache write
    /// </summary>
    internal enum StringCachePutResult
    {
        /// <summary>
        /// The in-memory copy is exactly the same; write skipped
        /// </summary>
        Duplicate,
        /// <summary>
        /// Write succeeded
        /// </summary>
        WriteComplete,
        /// <summary>
        /// An error occurred. Check the error sink on the implementation
        /// </summary>
        WriteFailed
    }

    /// <summary>
    ///  Implementations must not be tied or reliant on a specific Config instance
    /// </summary>
    internal interface IPersistentStringCache
    {
        StringCachePutResult TryPut(string key, string value);
        string Get(string key);


        DateTime? GetWriteTimeUtc(string key);
    }
}
