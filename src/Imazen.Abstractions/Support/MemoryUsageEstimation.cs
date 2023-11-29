using System.Runtime.InteropServices;

namespace Imazen.Common.Extensibility.Support;

public interface IEstimateAllocatedBytesRecursive
{
    /// <summary>
    /// Implementers should be able to track the estimated memory pressure of their allocation.
    /// Interned strings don't need to be counted byte-wise, for example.
    /// </summary>
    int EstimateAllocatedBytesRecursive { get; }
}

internal static class MemoryUsageEstimation
{
 
    public static int EstimateMemorySize<T>(this List<T>? obj, bool includeReference) where T: IEstimateAllocatedBytesRecursive
    {
        return (includeReference ? 8 : 0) + (obj == null
            ? 0
            : (16 + 8 + (obj.Capacity == 0 ? 0 : (24 + obj.Capacity * 8)) 
              + obj.Sum(x => x.EstimateAllocatedBytesRecursive)));
    }
    
    public static int EstimateMemorySize<T>(this IReadOnlyCollection<T>? obj, bool includeReference) where T: IEstimateAllocatedBytesRecursive
    {
        return (includeReference ? 8 : 0) + (obj == null
            ? 0
            : ((16 + 8 + obj.Count * 8) 
              + obj.Sum(x => x.EstimateAllocatedBytesRecursive)));
    }
    
    
    /// <summary>
    /// Can crash on structs, should only be used on primitive numeric types
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="includeReference"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static int EstimateMemorySize<T>(this T? obj, bool includeReference) where T : unmanaged 
    {
        return Marshal.SizeOf<T>() + 1 + (includeReference ? 8 : 0);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="includeReference"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static int EstimateMemorySize<T>(this T obj, bool includeReference) where T : unmanaged
    {
        return Marshal.SizeOf<T>() + 1 + (includeReference ? 8 : 0);
    }
    
    
    public static int EstimateMemorySize(this DateTimeOffset obj, bool includeReference)
    {
        return 8 + 8 + (includeReference ? 8 : 0);
    }
    public static int EstimateMemorySize(this DateTimeOffset? obj, bool includeReference)
    {
        return 1 + 8 + 8 + (includeReference ? 8 : 0);
    }
    
    public static int EstimateMemorySize(this string? s, bool includeReference)
    {
        return (includeReference ? 8 : 0) + (s == null ? 0 : (s.Length + 1) * sizeof(char) + 8 + 4);
    }
}