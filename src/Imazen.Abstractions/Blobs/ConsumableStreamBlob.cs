namespace Imazen.Abstractions.Blobs;

public enum DisposalPromise
{
    CallerDisposesStreamThenBlob = 1,
    CallerDisposesBlobOnly = 2
}

public interface IConsumableBlob : IDisposable
{
    /// <summary>
    /// The attributes of the blob, such as its content type
    /// </summary>
    IBlobAttributes Attributes { get; }
    /// <summary>
    /// If true, the stream is available and can be borrowed.
    /// It can only be taken once.
    /// </summary>
    bool StreamAvailable { get; }

    /// <summary>
    /// If not null, specifies the length of the stream in bytes
    /// </summary>
    long? StreamLength { get; }
    

    /// <summary>
    /// Borrows the stream from this blob wrapper. Can only be called once. The IConsumableBlob wrapper should not be disposed until after the stream is disposed as there may
    /// be associated resources that need to be cleaned up.
    /// </summary>
    /// <returns></returns>
    Stream BorrowStream(DisposalPromise callerPromises);
}

public interface IConsumableMemoryBlob : IConsumableBlob
{
    /// <summary>
    /// Borrows a read only view of the memory from this blob wrapper. The IConsumableMemoryBlob wrapper should not be disposed until the Memory is no longer in use.
    /// Can be called multiple times. May throw an ObjectDisposedException if the structure has been disposed already.
    /// </summary>
    ReadOnlyMemory<byte> BorrowMemory { get; }
}

public sealed class ConsumableStreamBlob(IBlobAttributes attrs, Stream stream, IDisposable? disposeAfterStream = null)
    : IConsumableBlob
{
    public IBlobAttributes Attributes { get; } = attrs;
    private Stream? _stream = stream;
    private DisposalPromise? disposalPromise = default;
    private bool disposed = false;

    public void Dispose()
    {
        disposed = true;
        if (disposalPromise != DisposalPromise.CallerDisposesStreamThenBlob)
        {
            _stream?.Dispose();
            _stream = null;
        }
    
        disposeAfterStream?.Dispose();
        disposeAfterStream = null;
    }

    public bool StreamAvailable => !disposalPromise.HasValue;
    public long? StreamLength { get; } = stream.CanSeek ? (int?)stream.Length : null;

    public Stream BorrowStream(DisposalPromise callerPromises)
    {
        if (disposed) throw new ObjectDisposedException("The ConsumableBlob has been disposed");
        if (!StreamAvailable) throw new InvalidOperationException("Stream has already been taken");
        disposalPromise = callerPromises;
        return _stream ?? throw new ObjectDisposedException("The ConsumableBlob has been disposed");
    }
}