namespace Imazen.Abstractions.Blobs;

/// <summary>
/// Not thread-safe.
/// </summary>
public interface IConsumableBlobPromise : IDisposable
{
    ValueTask<IConsumableBlob> IntoConsumableBlob();
}