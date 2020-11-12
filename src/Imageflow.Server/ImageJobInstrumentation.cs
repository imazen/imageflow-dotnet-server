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

        public int? SourceWidth { get; set; }
        public int? SourceHeight { get; set; }
        public int? FinalWidth { get; set; }
        public int? FinalHeight { get; set; }
        public long TotalTicks { get; set; }
        public long DecodeTicks { get; set; }
        public long EncodeTicks { get; set; }
        public string SourceFileExtension { get; set; }
        public string ImageDomain { get; set; }
        public string PageDomain { get; set; }
        public IEnumerable<string> FinalCommandKeys { get; set; }
    }
}