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
        
        private readonly List<PathMapping> mappedPaths = new List<PathMapping>();
        
        public IReadOnlyCollection<PathMapping> MappedPaths => mappedPaths;

        public bool MapWebRoot { get; set; } = false;
        
        public string DefaultCacheControlString { get; set; }
        
        internal readonly List<UrlHandler<Action<UrlEventArgs>>> Rewrite = new List<UrlHandler<Action<UrlEventArgs>>>();

        internal readonly List<UrlHandler<Func<UrlEventArgs, bool>>> PreRewriteAuthorization = new List<UrlHandler<Func<UrlEventArgs, bool>>>();

        internal readonly List<UrlHandler<Func<UrlEventArgs, bool>>> PostRewriteAuthorization = new List<UrlHandler<Func<UrlEventArgs, bool>>>();

        internal readonly Dictionary<string, string> CommandDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Use this to add default command values if they are missing. Does not affect image requests with no querystring.
        /// Example: AddCommandDefault("down.colorspace", "srgb") reverts to ImageResizer's legacy behavior in scaling shadows and highlights.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ImageflowMiddlewareOptions AddCommandDefault(string key, string value)
        {
            if (CommandDefaults.ContainsKey(key)) throw new ArgumentOutOfRangeException(nameof(key), "A default has already been added for this key");
            CommandDefaults[key] = value;
            return this;
        }
        public ImageflowMiddlewareOptions AddRewriteHandler(string pathPrefix, Action<UrlEventArgs> handler)
        {
            Rewrite.Add(new UrlHandler<Action<UrlEventArgs>>(pathPrefix, handler));
            return this;
        }
        public ImageflowMiddlewareOptions AddPreRewriteAuthorizationHandler(string pathPrefix, Func<UrlEventArgs, bool> handler)
        {
            PreRewriteAuthorization.Add(new UrlHandler<Func<UrlEventArgs, bool>>(pathPrefix, handler));
            return this;
        }
        public ImageflowMiddlewareOptions AddPostRewriteAuthorizationHandler(string pathPrefix, Func<UrlEventArgs, bool> handler)
        {
            PostRewriteAuthorization.Add(new UrlHandler<Func<UrlEventArgs, bool>>(pathPrefix, handler));
            return this;
        }

        public ImageflowMiddlewareOptions SetMapWebRoot(bool value)
        {
            MapWebRoot = value;
            return this;
        }
        
        public ImageflowMiddlewareOptions MapPath(string virtualPath, string physicalPath)
        {
            mappedPaths.Add(new PathMapping(virtualPath,physicalPath));
            return this;
        }
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

        /// <summary>
        /// Use "public, max-age=2592000" to cache for 30 days and cache on CDNs and proxies.
        /// </summary>
        /// <param name="cacheControlString"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ImageflowMiddlewareOptions SetDefaultCacheControlString(string cacheControlString)
        {
            DefaultCacheControlString = cacheControlString;
            return this;
        }
    }
}