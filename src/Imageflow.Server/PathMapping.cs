namespace Imageflow.Server
{
    public struct PathMapping
    {
        public PathMapping(string virtualPath, string physicalPath)
        {
            VirtualPath = virtualPath;
            PhysicalPath = physicalPath.TrimEnd('/','\\');
        }
        public string VirtualPath { get; }
        public string PhysicalPath { get; }
    }
}