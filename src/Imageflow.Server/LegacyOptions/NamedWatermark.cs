using Imageflow.Fluent;
using Imazen.Routing.Promises.Pipelines.Watermarking;

namespace Imageflow.Server
{
    public class NamedWatermark(string? name, string virtualPath, WatermarkOptions watermark)
        : IWatermark
    {
        public string? Name { get; } = name;
        public string VirtualPath { get; } = virtualPath;
        public WatermarkOptions Watermark { get; } = watermark;

        // convert from IWatermark to NamedWatermark
        internal static NamedWatermark From(IWatermark watermark)
        {
            return new NamedWatermark(watermark.Name, watermark.VirtualPath, watermark.Watermark);
        }
        
    }
}