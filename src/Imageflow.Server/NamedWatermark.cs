using Imageflow.Fluent;

namespace Imageflow.Server
{
    public class NamedWatermark
    {
        public NamedWatermark(string name, string virtualPath, WatermarkOptions watermark)
        {
            Name = name;
            VirtualPath = virtualPath;
            Watermark = watermark;
        }
        public string Name { get; }
        public string VirtualPath { get; }
        public WatermarkOptions Watermark { get; }
    }
}