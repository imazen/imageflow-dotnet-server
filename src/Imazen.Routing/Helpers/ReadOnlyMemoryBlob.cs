using System.Buffers;
using CommunityToolkit.HighPerformance;
using Imazen.Common.Extensibility.Support;
using CommunityToolkit.HighPerformance.Streams;

namespace Imazen.Abstractions.Blobs
{

        
    public sealed class ConsumableMemoryBlob : IConsumableMemoryBlob
    {
        public IBlobAttributes Attributes { get; }

        /// <summary>
        /// Creates a consumable wrapper over owner-less memory (I.E. owned by the garbage collector - it cannot be manually disposed)
        /// </summary>
        /// <param name="attrs"></param>
        /// <param name="memory"></param>
        public ConsumableMemoryBlob(IBlobAttributes attrs, ReadOnlyMemory<byte> memory): this(attrs, memory, null)
        {
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="attrs"></param>
        /// <param name="memory"></param>
        /// <param name="owner"></param>
        internal ConsumableMemoryBlob(IBlobAttributes attrs, ReadOnlyMemory<byte> memory, IMemoryOwner<byte>? owner = null)
        {
            this.Attributes = attrs;
            this.memory = memory;
            this.owner = owner;
        }
        public void Dispose()
        {
            disposed = true;
            memory = default;
            if (disposalPromise == DisposalPromise.CallerDisposesBlobOnly)
            {
                streamBorrowed?.Dispose();
                streamBorrowed = null;
            }
            owner?.Dispose();
            owner = null;
        }
        private IMemoryOwner<byte>? owner;
        private ReadOnlyMemory<byte> memory;
        private bool disposed = false;
        private Stream? streamBorrowed;
        private DisposalPromise? disposalPromise = default;
        public ReadOnlyMemory<byte> BorrowMemory
        {
            get
            {
                if (disposed) throw new ObjectDisposedException("The ConsumableMemoryBlob has been disposed");
                return memory;
            }
        }
        public bool StreamAvailable => !disposed && streamBorrowed == null;
        public long? StreamLength => !disposed ? memory.Length : default;
        public Stream BorrowStream(DisposalPromise callerPromises)
        {
            if (disposed) throw new ObjectDisposedException("The ConsumableMemoryBlob has been disposed. .BorrowStream is an invalid operation on a disposed object");
            if (this.disposalPromise != null) throw new InvalidOperationException("The stream has already been taken");
            streamBorrowed = memory.AsStream();
            disposalPromise = callerPromises;
            return streamBorrowed;
        }
    }

    public sealed class ReusableReadOnlyMemoryBlob : IReusableBlob
    {
        public IBlobAttributes Attributes { get; }
        private ReadOnlyMemory<byte>? data;
        private bool disposed = false;
        public TimeSpan CreationDuration { get; init; }
        public DateTime CreationCompletionUtc { get; init; } = DateTime.UtcNow;

        private ReadOnlyMemory<byte> Memory => disposed || data == null
            ? throw new ObjectDisposedException("The ReusableReadOnlyMemoryBlob has been disposed")
            : data.Value;
        
        public ReusableReadOnlyMemoryBlob(ReadOnlyMemory<byte> data, IBlobAttributes metadata, TimeSpan creationDuration, int? backingAllocationSize = null)
        {
            CreationDuration = creationDuration;
            this.data = data;
            this.Attributes = metadata;
            var backingSize = backingAllocationSize ?? data.Length;
            if (backingSize < data.Length)
            {
                throw new ArgumentException("backingAllocationSize must be at least as large as data.Length");
            }
            // Precalculate since it will be called often
            EstimateAllocatedBytesRecursive = 
                24 + Attributes.EstimateAllocatedBytesRecursive
                   + 24 + (backingSize);
        }
        
        public int EstimateAllocatedBytesRecursive { get; }
        
        
        public IConsumableBlob GetConsumable()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("The ReusableReadOnlyMemoryBlob has been disposed");
            }
            return new ConsumableMemoryBlob(Attributes, Memory);
        }
        
        public bool IsDisposed => disposed;

        public long StreamLength => IsDisposed
            ? throw new ObjectDisposedException("The ReusableReadOnlyMemoryBlob has been disposed")
            : data!.Value.Length;
        public void Dispose()
        {
            disposed = true;
            data = null;
        }

    }
}