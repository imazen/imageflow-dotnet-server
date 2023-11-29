using Imageflow.Fluent;

namespace Imazen.Routing.Promises.Pipelines.Watermarking;

public interface IWatermark
{
    string? Name { get; }
    string VirtualPath { get; }
    WatermarkOptions Watermark { get; }
}

public record WatermarkWithPath(string? Name, string VirtualPath, WatermarkOptions Watermark)
    : IWatermark
{
    public static WatermarkWithPath FromIWatermark(IWatermark watermark)
    {
        return new WatermarkWithPath(watermark.Name, watermark.VirtualPath, watermark.Watermark);
    }
} 
