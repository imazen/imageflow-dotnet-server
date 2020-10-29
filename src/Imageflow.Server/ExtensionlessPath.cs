using System;

namespace Imageflow.Server
{
    internal struct ExtensionlessPath
    {
        internal string Prefix { get; set; }
        
        internal StringComparison PrefixComparison { get; set; }
    }
}