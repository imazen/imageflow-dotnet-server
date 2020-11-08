using System.Collections.Generic;

namespace Imazen.Common.Instrumentation.Support
{
    interface IPercentileProviderSink
    {
        void Report(long value);
        long GetPercentile(float percentile);
        long[] GetPercentiles(IEnumerable<float> percentiles);
    }
}