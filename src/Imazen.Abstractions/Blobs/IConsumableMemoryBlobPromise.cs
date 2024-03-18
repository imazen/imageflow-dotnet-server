namespace Imazen.Abstractions.Blobs;

/// <summary>
/// Not thread safe
/// </summary>
public interface IConsumableMemoryBlobPromise: IDisposable
{
    ValueTask<IConsumableMemoryBlob> IntoConsumableMemoryBlob();
}