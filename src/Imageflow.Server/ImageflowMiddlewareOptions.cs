using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Imageflow.Server
{
    public class ImageflowMiddlewareOptions
    {
        public ImageflowMiddlewareOptions()
        {
        }

        public bool AllowMemoryCaching { get; set; } = false;

        public TimeSpan MemoryCacheSlidingExpiration { get; set; } = TimeSpan.FromHours(24);
        public TimeSpan DistributedCacheSlidingExpiration { get; set; } = TimeSpan.FromHours(24);
        public bool AllowDiskCaching { get; set; } = true;
        public bool AllowDistributedCaching { get; set; } = false;

        private readonly List<NamedWatermark> namedWatermarks = new List<NamedWatermark>();
        public IReadOnlyCollection<NamedWatermark> NamedWatermarks => namedWatermarks;

        public ImageflowMiddlewareOptions AddWatermark(NamedWatermark watermark)
        {
            if (namedWatermarks.Any(w => w.Name.Equals(watermark.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A watermark already exists by the name {watermark.Name}");
            }
            namedWatermarks.Add(watermark);
            return this;
        }
        public ImageflowMiddlewareOptions SetAllowMemoryCaching(bool value)
        {
            this.AllowMemoryCaching = value;
            return this;
        }
        public ImageflowMiddlewareOptions SetMemoryCacheSlidingExpiration(TimeSpan value)
        {
            this.MemoryCacheSlidingExpiration = value;
            return this;
        }
        public ImageflowMiddlewareOptions SetDistributedCacheSlidingExpiration(TimeSpan value)
        {
            this.DistributedCacheSlidingExpiration = value;
            return this;
        }
        public ImageflowMiddlewareOptions SetAllowDiskCaching(bool value)
        {
            this.AllowDiskCaching = value;
            return this;
        }
        public ImageflowMiddlewareOptions SetAllowDistributedCaching(bool value)
        {
            this.AllowDistributedCaching = value;
            return this;
        }
    }
}