using Imageflow.Fluent;

namespace Imageflow.Server;

internal static class PerformanceDetailsExtensions{
        
    private static long GetWallMicroseconds(this PerformanceDetails d, Func<string, bool> nodeFilter)
    {
        long totalMicroseconds = 0;
        foreach (var frame in d.Frames)
        {
            foreach (var node in frame.Nodes.Where(n => nodeFilter(n.Name)))
            {
                totalMicroseconds += node.WallMicroseconds;
            }
        }

        return totalMicroseconds;
    }
        
  
        
    public static long GetTotalWallTicks(this PerformanceDetails d) =>
        d.GetWallMicroseconds(n => true) * TimeSpan.TicksPerSecond / 1000000;
        
    public static long GetEncodeWallTicks(this PerformanceDetails d) =>
        d.GetWallMicroseconds(n => n == "primitive_encoder") * TimeSpan.TicksPerSecond / 1000000;
        
    public static long GetDecodeWallTicks(this PerformanceDetails d) =>
        d.GetWallMicroseconds(n => n == "primitive_decoder") * TimeSpan.TicksPerSecond / 1000000;
}