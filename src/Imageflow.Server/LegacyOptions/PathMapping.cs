using Imazen.Routing.Layers;

namespace Imageflow.Server
{
    public readonly struct PathMapping : IPathMapping
    {
        public PathMapping(string virtualPath, string physicalPath)
        {
            VirtualPath = virtualPath;
            PhysicalPath = physicalPath.TrimEnd('/','\\');
            IgnorePrefixCase = false;
        }
        
        public PathMapping(string virtualPath, string physicalPath, bool ignorePrefixCase)
        {
            VirtualPath = virtualPath;
            PhysicalPath = physicalPath.TrimEnd('/','\\');
            IgnorePrefixCase = ignorePrefixCase;
        }
        public string VirtualPath { get; }
        public string PhysicalPath { get; }
        public bool IgnorePrefixCase { get; }
        /// <summary>
        /// Duplicate of VirtualPath for IStringAndComparison
        /// </summary>
        public string StringToCompare => VirtualPath;
        /// <summary>
        /// If IgnorePrefixCase is true, returns OrdinalIgnoreCase, otherwise Ordinal
        /// </summary>
        public StringComparison StringComparison => IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }
}