namespace Imageflow.Server
{
    public struct PathMapping
    {
        public PathMapping(string virtualPath, string physicalPath, bool ignoreCase = false)
        {
            VirtualPath = virtualPath;
            PhysicalPath = physicalPath.TrimEnd('/','\\');
            IgnoreCase = ignoreCase;
        }

        public string VirtualPath { get; }
        public string PhysicalPath { get; }
        public bool IgnoreCase { get; }
    }
}
