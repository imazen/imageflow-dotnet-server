using Imazen.Common.Extensibility.Support;

namespace Imazen.Abstractions.Blobs;

/// <summary>
/// Indicates that the implementation is thread-safe, and that .BorrowStream (and .BorrowMemory if IConsumableMemoryBlob) can be called multiple times.
/// </summary>
public interface IReusableBlob : IConsumableBlob, IEstimateAllocatedBytesRecursive
{
}