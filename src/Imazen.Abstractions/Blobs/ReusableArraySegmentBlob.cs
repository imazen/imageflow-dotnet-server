using Imazen.Common.Extensibility.Support;

namespace Imazen.Abstractions.Blobs
{
    /// <summary>
    /// Provides access to a blob that can be shared and
    /// reused by multiple threads,
    /// and supports creation of multiple read streams to the backing memory.
    /// The instance should never be disposed except by the Imageflow runtime itself.
    /// </summary>
    public interface IReusableBlob : IDisposable, IEstimateAllocatedBytesRecursive
    {
        IBlobAttributes Attributes { get; }
        long StreamLength { get; }
        IConsumableBlob GetConsumable();
    }
    
    public interface ISimpleReusableBlob: IReusableBlob
    {
        internal Stream CreateReadStream();
        internal bool IsDisposed { get; }
    }
    public sealed class ReusableArraySegmentBlob : ISimpleReusableBlob
    {
        public IBlobAttributes Attributes { get; }
        private ArraySegment<byte>? data;
        private bool disposed = false;
        public TimeSpan CreationDuration { get; init; }
        public DateTime CreationCompletionUtc { get; init; } = DateTime.UtcNow;

        public ReusableArraySegmentBlob(ArraySegment<byte> data, IBlobAttributes metadata, TimeSpan creationDuration)
        {
            CreationDuration = creationDuration;
            this.data = data;
            this.Attributes = metadata;
            // Precalculate since it will be called often
            EstimateAllocatedBytesRecursive = 
                24 + Attributes.EstimateAllocatedBytesRecursive
                   + 24 + (data.Array?.Length ?? 0);
        }
        
        public int EstimateAllocatedBytesRecursive { get; }
        
        public Stream CreateReadStream()
        {
            if (IsDisposed || data == null)
            {
                throw new ObjectDisposedException("The ReusableArraySegmentBlob has been disposed");
            }
            var d = this.data.Value;
            if (d.Count == 0 || d.Array == null)
            {
                return new MemoryStream(0);
            }
            return new MemoryStream(d.Array, d.Offset, d.Count, false);
        }
        
        public IConsumableBlob GetConsumable()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("The ReusableArraySegmentBlob has been disposed");
            }
            return new ConsumableWrapperForReusable(this);
        }
        
        public bool IsDisposed => disposed;
        public long StreamLength => IsDisposed ? throw new ObjectDisposedException("The ReusableArraySegmentBlob has been disposed") : data?.Count ?? 0;
        public void Dispose()
        {
            disposed = true;
            data = null;
        }
    }

    internal sealed class ConsumableWrapperForReusable(ISimpleReusableBlob reusable) : IConsumableBlob
    {
         //private Stream? stream;
        private DisposalPromise? disposalPromise = default;
        private bool Taken => disposalPromise.HasValue;
        private bool disposed = false;
        private Stream? stream;
        public void Dispose()
        {
            disposed = true;
            if (disposalPromise != DisposalPromise.CallerDisposesBlobOnly)
            {
                stream?.Dispose();
                stream = null;
            }
        }

        public IBlobAttributes Attributes =>
            reusable.IsDisposed
                ? throw new ObjectDisposedException("The underlying reusable blob has been disposed")
                : disposed ? throw new ObjectDisposedException("The consumable wrapper has been disposed") 
                    : reusable.Attributes;

        public bool StreamAvailable => !Taken && !disposed && !(reusable?.IsDisposed ?? true);
        public long? StreamLength =>
            reusable.IsDisposed
                ? throw new ObjectDisposedException("The underlying reusable blob has been disposed")
                : disposed ? throw new ObjectDisposedException("The consumable wrapper has been disposed") 
                    : reusable.StreamLength;


        public Stream BorrowStream(DisposalPromise callerPromises)
        {
            if (Taken)
            {
                throw new InvalidOperationException("The stream has already been taken");
            }
            disposalPromise = callerPromises;
            var s = reusable.IsDisposed
                ? throw new ObjectDisposedException("The underlying reusable blob has been disposed")
                : disposed ?
                    throw new ObjectDisposedException("The consumable wrapper has been disposed")
                    : reusable.CreateReadStream();
            
            stream = s;
            return s;
        }
    }
}