using System.Buffers;
using Imageflow.Fluent;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Promises.Pipelines.Watermarking;




public record WatermarkingLogicOptions(Func<string, WatermarkWithPath?>? LookupWatermark, Func<IRequestSnapshot, IList<WatermarkWithPath>?, IList<WatermarkWithPath>?>? MutateWatermarks)
{


    public IList<WatermarkWithPath>? GetAppliedWatermarks(IRequestSnapshot request)
    {
        IList<WatermarkWithPath>? appliedWatermarks = null;
        if (request.QueryString?.TryGetValue("watermark", out var watermarkValues) == true)
        {
            foreach (var name in watermarkValues.Select(s => s?.Trim(' ')))
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (LookupWatermark == null)
                {
                    throw new InvalidOperationException(
                        $"watermark {name} was referenced from the querystring but no watermarks were registered");
                }

                var watermark = LookupWatermark(name!);
                if (watermark == null)
                {
                    throw new InvalidOperationException(
                        $"watermark {name} was referenced from the querystring but no watermark by that name was found");
                }

                appliedWatermarks ??= [];
                appliedWatermarks.Add(watermark);
            }
        }

        // After we've populated the defaults, run the event handlers for custom watermarking logic

        if (MutateWatermarks != null)
        {
            appliedWatermarks = MutateWatermarks(request, appliedWatermarks);
        }

        return appliedWatermarks;
    }
    
    // write cache key basis for watermarking
    internal static void WriteWatermarkingCacheKeyBasis(IList<WatermarkWithPath>? appliedWatermarks, IBufferWriter<byte> writer)
    {
        if (appliedWatermarks == null) return;
        foreach (var watermark in appliedWatermarks)
        {
            writer.WriteWtf(watermark.Name ?? "(null)");
            writer.WriteWtf(watermark.VirtualPath ?? "(null)");
            writer.WriteWtf(watermark.Watermark.Gravity?.XPercent);
            writer.WriteWtf(watermark.Watermark.Gravity?.YPercent);
            switch (watermark.Watermark.FitBox)
            {
                case WatermarkMargins margins:
                    writer.WriteWtf("margins");
                    writer.WriteWtf(margins.Left);
                    writer.WriteWtf(margins.Top);
                    writer.WriteWtf(margins.Right);
                    writer.WriteWtf(margins.Bottom);
                    break;
                case WatermarkFitBox fitBox:
                    writer.WriteWtf("fit-box");
                    writer.WriteWtf(fitBox.X1);
                    writer.WriteWtf(fitBox.Y1);
                    writer.WriteWtf(fitBox.X2);
                    writer.WriteWtf(fitBox.Y2);
                    break;
                case null:
                    writer.WriteWtf("(null)");
                    break;
                default:
                    throw new InvalidOperationException("Unknown watermark fit box type");
            }
            writer.WriteWtf(watermark.Watermark.FitMode);
            writer.WriteWtf(watermark.Watermark.MinCanvasWidth);
            writer.WriteWtf(watermark.Watermark.MinCanvasHeight);
            writer.WriteWtf(watermark.Watermark.Opacity);
            writer.WriteWtf(watermark.Watermark.Hints?.DownFilter);
            writer.WriteWtf(watermark.Watermark.Hints?.UpFilter);
            writer.WriteWtf(watermark.Watermark.Hints?.InterpolationColorspace);
            writer.WriteWtf(watermark.Watermark.Hints?.ResampleWhen);
            writer.WriteWtf(watermark.Watermark.Hints?.SharpenPercent);
            writer.WriteWtf(watermark.Watermark.Hints?.SharpenWhen);
        }
    }
    
}