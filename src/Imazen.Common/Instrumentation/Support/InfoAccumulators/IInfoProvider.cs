namespace Imazen.Common.Instrumentation.Support.InfoAccumulators
{
    public interface IInfoProvider
    {
        void Add(IInfoAccumulator accumulator);
    }
}