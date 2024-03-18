namespace Imazen.Abstractions.Blobs;

public interface IConsumableMemoryBlob : IConsumableBlob
{
    /// <summary>
    /// Borrows a read only view of the memory from this blob wrapper. The IConsumableMemoryBlob wrapper should not be disposed until the Memory is no longer in use.
    /// Can be called multiple times. May throw an ObjectDisposedException if the structure has been disposed already.
    /// </summary>
    ReadOnlyMemory<byte> BorrowMemory { get; }
}