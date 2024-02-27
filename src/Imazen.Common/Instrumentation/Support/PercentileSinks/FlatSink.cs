﻿using Imazen.Common.Instrumentation.Support.Clamping;

namespace Imazen.Common.Instrumentation.Support.PercentileSinks
{
    class FlatSink : IPercentileProviderSink
    {
        public FlatSink(long maxValue)
        {

            clamp = new SegmentClamping()
            {
                MinValue = 0,
                MaxValue = maxValue, //400 megapixels
                Segments = new [] {
                    new SegmentPrecision { Above = 0, Loss = 1 }
                }
            };
            clamp.Validate();
        }

        readonly SegmentClamping clamp;
        readonly CountMinSketch<AddMulModHash> table = new CountMinSketch<AddMulModHash>(1279, 4, AddMulModHash.DeterministicDefault());

        public void Report(long value)
        {
            table.InterlockedAdd((uint)clamp.Clamp(value), 1);
        }

        public long[] GetPercentiles(IEnumerable<float> percentiles)
        {
            return table.GetPercentiles(percentiles, clamp);
        }

        public long GetPercentile(float percentile)
        {
            return table.GetPercentile(percentile, clamp);
        }

    }

}
