namespace Imazen.Abstractions.Blobs
{
    /// <summary>
    /// Provides access to a blob stream that can only be used once,
    /// and not shared. We should figure out ownership and disposal semantics.
    /// </summary>
    public interface IBlobWrapper : IDisposable //TODO:  Change to IAsyncDisposable if anyone needs that (network streams)?
    {
        IBlobAttributes Attributes { get; }

        bool IsNativelyReusable { get; }
        
        bool CanTakeReusable { get; }
        
        ValueTask<IReusableBlob> TakeReusable(IReusableBlobFactory factory, CancellationToken cancellationToken = default);
        
        ValueTask EnsureReusable(IReusableBlobFactory factory, CancellationToken cancellationToken = default);
        
        bool CanTakeConsumable { get; }
        
        IConsumableBlob TakeConsumable();
        
        bool CanCreateConsumable { get; }
        
        long? EstimateAllocatedBytes { get; }
        
        ValueTask<IConsumableBlob> CreateConsumable(IReusableBlobFactory factory, CancellationToken cancellationToken = default);
        IConsumableBlob MakeOrTakeConsumable();
    }

    /// <summary>
    /// TODO: make this properly thread-safe
    /// </summary>
    public class BlobWrapper : IBlobWrapper
    {
        private IConsumableBlob? consumable;
        private IReusableBlob? reusable;
        internal DateTime CreatedAtUtc { get; }
        internal LatencyTrackingZone? LatencyZone { get; set; }
        
        public BlobWrapper(LatencyTrackingZone? latencyZone, IConsumableBlob consumable)
        {
            this.consumable = consumable;
            this.Attributes = consumable.Attributes;
            CreatedAtUtc = DateTime.UtcNow;
            LatencyZone = latencyZone;
        }
        public BlobWrapper(LatencyTrackingZone? latencyZone, IReusableBlob reusable)
        {
            this.reusable = reusable;
            this.Attributes = reusable.Attributes;
            CreatedAtUtc = DateTime.UtcNow;
            LatencyZone = latencyZone;
        }
        [Obsolete("Use the constructor that takes a first parameter of LatencyTrackingZone, so that you " +
                  "can allow Imageflow Server to apply intelligent caching logic to this blob.")]
        public BlobWrapper(IConsumableBlob consumable)
        {
            this.consumable = consumable;
            this.Attributes = consumable.Attributes;
            CreatedAtUtc = DateTime.UtcNow;
        }
        
        

        public IBlobAttributes Attributes { get; }
        public bool IsNativelyReusable => reusable != null;

        public bool CanTakeReusable => reusable != null || consumable != null;

        public long? EstimateAllocatedBytes => reusable?.EstimateAllocatedBytesRecursive;
        
        public async ValueTask<IReusableBlob> TakeReusable(IReusableBlobFactory factory, CancellationToken cancellationToken = default)
        {
            if (reusable != null)
            {
                var r = reusable;
                reusable = null;
                return r;
            }

            if (consumable != null)
            {
                IConsumableBlob c = consumable;
                if (c != null)
                {
                    try
                    {
                        consumable = null;
                        if (!c.StreamAvailable)
                        {
                            throw new InvalidOperationException("Cannot create a reusable blob from this wrapper, the consumable stream has already been taken");
                        }
                        return await factory.ConsumeAndCreateReusableCopy(c, cancellationToken);
                    }
                    finally
                    {
                        c.Dispose();
                    }
                }
            }

            throw new InvalidOperationException("Cannot take or create a reusable blob from this wrapper, it is empty");
        }

        public async ValueTask EnsureReusable(IReusableBlobFactory factory, CancellationToken cancellationToken = default)
        {
            if (reusable != null) return;
            if (consumable != null)
            {
                IConsumableBlob c = consumable;
                if (c != null)
                {
                    try
                    {
                        consumable = null;
                        if (!c.StreamAvailable)
                        {
                            throw new InvalidOperationException("Cannot create a reusable blob from this wrapper, the consumable stream has already been taken");
                        }

                        reusable = await factory.ConsumeAndCreateReusableCopy(c, cancellationToken);
                        return;
                    }
                    finally
                    {
                        c.Dispose();
                    }
                }
            }

            throw new InvalidOperationException("Cannot take or create a reusable blob from this wrapper, it is empty");
        }

        public bool CanTakeConsumable => consumable != null;
        
        public IConsumableBlob TakeConsumable()
        {
            if (consumable != null)
            {
                var c = consumable;
                consumable = null;
                return c;
            }

            if (reusable != null)
            {
                throw new InvalidOperationException("Try TakeOrCreateConsumable(), -cannot take a consumable blob from this wrapper, it only contains a reusable one");
              
            }
            throw new InvalidOperationException("Cannot take a consumable blob from this wrapper, it is empty of both consumable and reusable");
        }
        
        public bool CanCreateConsumable => consumable != null || reusable != null;
     
        public async ValueTask<IConsumableBlob> CreateConsumable(IReusableBlobFactory factory, CancellationToken cancellationToken = default)
        {
            reusable ??= await TakeReusable(factory, cancellationToken);
            return reusable.GetConsumable();
            
        }

        public IConsumableBlob MakeOrTakeConsumable()
        {
            if (reusable != null)
            {
                var r = reusable;
                reusable = null;
                return r.GetConsumable();
            }

            return TakeConsumable();
        }


        public void Dispose()
        {
            consumable?.Dispose();
            reusable?.Dispose();
        }
    }




}
        