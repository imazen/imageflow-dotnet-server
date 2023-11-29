namespace Imazen.Common.Instrumentation.Support.PercentileSinks
{
    interface IPercentileProviderSink
    {
        void Report(long value);
        long GetPercentile(float percentile);
        long[] GetPercentiles(IEnumerable<float> percentiles);
    }
}