namespace Imazen.Routing.Serving;

public interface IPerformanceTracker
{
    void IncrementCounter(string name);
}
internal class NullPerformanceTracker : IPerformanceTracker{
    public void IncrementCounter(string name){}
}