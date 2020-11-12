using System.Collections.Generic;
using System.Linq;
using Imageflow.Fluent;
using Imazen.Common.Instrumentation;

namespace Imageflow.Server
{
    internal struct ImageJobInstrumentation: IImageJobInstrumentation
    {
        public ImageJobInstrumentation(BuildJobResult jobResult)
        {
            FinalWidth = jobResult.EncodeResults.FirstOrDefault()?.Width;
            FinalHeight = jobResult.EncodeResults.FirstOrDefault()?.Height;
            TotalTicks = jobResult.PerformanceDetails.GetTotalWallTicks();
            DecodeTicks = jobResult.PerformanceDetails.GetDecodeWallTicks();
            EncodeTicks = jobResult.PerformanceDetails.GetEncodeWallTicks();
            SourceFileExtension = jobResult.DecodeResults.FirstOrDefault()?.PreferredExtension;
            SourceHeight = jobResult.DecodeResults.FirstOrDefault()?.Height;
            SourceWidth = jobResult.DecodeResults.FirstOrDefault()?.Width;
            ImageDomain = null;
            PageDomain = null;
            FinalCommandKeys = null;
        }

        public int? SourceWidth { get; }
        public int? SourceHeight { get; }
        public int? FinalWidth { get; }
        public int? FinalHeight { get; }
        public long TotalTicks { get; }
        public long DecodeTicks { get; }
        public long EncodeTicks { get; }
        public string SourceFileExtension { get; }
        public string ImageDomain { get; set; }
        public string PageDomain { get; set; }
        public IEnumerable<string> FinalCommandKeys { get; set; }
    }
}