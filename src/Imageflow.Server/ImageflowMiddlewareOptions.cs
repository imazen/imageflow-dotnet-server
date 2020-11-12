using System;
using System.Collections.Generic;
using System.Linq;
using Imageflow.Fluent;

namespace Imageflow.Server
{
    public class ImageflowMiddlewareOptions
    {
        internal string LicenseKey { get; set; }
        internal EnforceLicenseWith EnforcementMethod { get; set; } = EnforceLicenseWith.Http422Error;
        
        internal Licensing Licensing { get; set; }

        public string MyOpenSourceProjectUrl { get; set; } = "https://i-need-a-license.com";

        public bool AllowMemoryCaching { get; set; }

        public bool AllowSqliteCaching { get; set; }


        public TimeSpan MemoryCacheSlidingExpiration { get; set; } = TimeSpan.FromHours(24);
        public TimeSpan DistributedCacheSlidingExpiration { get; set; } = TimeSpan.FromHours(24);
        public bool AllowDiskCaching { get; set; } = true;
        public bool AllowDistributedCaching { get; set; }
        
        internal CacheBackend ActiveCacheBackend { get; set; }

        private readonly List<NamedWatermark> namedWatermarks = new List<NamedWatermark>();
        public IReadOnlyCollection<NamedWatermark> NamedWatermarks => namedWatermarks;
        
        private readonly List<PathMapping> mappedPaths = new List<PathMapping>();
        
        public IReadOnlyCollection<PathMapping> MappedPaths => mappedPaths;

        public bool MapWebRoot { get; set; }
        
        internal bool ShowDiagnosticsLocalhost { get; set; }
        internal string DiagnosticsPassword { get; set;  }

        public bool UsePresetsExclusively { get; set; }
        
        public string DefaultCacheControlString { get; set; }
        
        public RequestSignatureOptions RequestSignatureOptions { get; set; }
        public SecurityOptions JobSecurityOptions { get; set; }
        
        internal readonly List<UrlHandler<Action<UrlEventArgs>>> Rewrite = new List<UrlHandler<Action<UrlEventArgs>>>();

        internal readonly List<UrlHandler<Func<UrlEventArgs, bool>>> PreRewriteAuthorization = new List<UrlHandler<Func<UrlEventArgs, bool>>>();

        internal readonly List<UrlHandler<Func<UrlEventArgs, bool>>> PostRewriteAuthorization = new List<UrlHandler<Func<UrlEventArgs, bool>>>();

        internal readonly List<UrlHandler<Action<WatermarkingEventArgs>>> Watermarking = new List<UrlHandler<Action<WatermarkingEventArgs>>>();


        internal readonly Dictionary<string, string> CommandDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        internal readonly Dictionary<string, PresetOptions> Presets = new Dictionary<string, PresetOptions>(StringComparer.OrdinalIgnoreCase);
        
        internal readonly List<ExtensionlessPath> ExtensionlessPaths = new List<ExtensionlessPath>();
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
        
        public ImageflowMiddlewareOptions AddPreset(PresetOptions preset)
        {
            if (Presets.ContainsKey(preset.Name)) throw new ArgumentOutOfRangeException(nameof(preset), "A preset by this name has already been added");
            Presets[preset.Name] = preset;
            return this;
        }

        public ImageflowMiddlewareOptions HandleExtensionlessRequestsUnder(string prefix, StringComparison prefixComparison = StringComparison.Ordinal)
        {
            ExtensionlessPaths.Add(new ExtensionlessPath() { Prefix = prefix, PrefixComparison = prefixComparison});
            return this;
        }

        public ImageflowMiddlewareOptions SetDiagnosticsPageAccess(bool allowLocalhostAccess, string password)
        {
            DiagnosticsPassword = password;
            ShowDiagnosticsLocalhost = allowLocalhostAccess;
            return this; 
        }

        /// <summary>
        /// Use this if you are complying with the AGPL v3 and open-sourcing your project.
        /// Provide the URL to your version control system or source code download page.
        /// Use .SetLicenseKey() instead if you are not open-sourcing your project.
        /// </summary>
        /// <param name="myOpenSourceProjectUrl">Provide the URL to your version control
        /// system or source code download page.</param>
        /// <returns></returns>
        public ImageflowMiddlewareOptions SetMyOpenSourceProjectUrl(string myOpenSourceProjectUrl)
        {
            MyOpenSourceProjectUrl = myOpenSourceProjectUrl;
            return this;
        }
        
        /// <summary>
        /// If you do not call this, Imageflow.Server will watermark image requests with a red dot. 
        ///
        /// If you are open-sourcing your project and complying with the AGPL v3, you can call
        /// .SetMyOpenSourceProjectUrl() instead.
        /// </summary>
        /// <param name="licenseKey"></param>
        /// <param name="enforcementMethod"></param>
        /// <returns></returns>
        public ImageflowMiddlewareOptions SetLicenseKey(EnforceLicenseWith enforcementMethod, string licenseKey)
        {
            EnforcementMethod = enforcementMethod;
            LicenseKey = licenseKey;
            return this;
        }
        
        public ImageflowMiddlewareOptions SetRequestSignatureOptions(RequestSignatureOptions options)
        {
            RequestSignatureOptions = options;
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

        public ImageflowMiddlewareOptions AddWatermarkingHandler(string pathPrefix, Action<WatermarkingEventArgs> handler)
        {
            Watermarking.Add(new UrlHandler<Action<WatermarkingEventArgs>>(pathPrefix, handler));
            return this;
        }
        
        public ImageflowMiddlewareOptions SetMapWebRoot(bool value)
        {
            MapWebRoot = value;
            return this;
        }
        
        public ImageflowMiddlewareOptions SetJobSecurityOptions(SecurityOptions securityOptions)
        {
            JobSecurityOptions = securityOptions;
            return this;
        }
        
        /// <summary>
        /// If true, querystrings will be discarded except for their preset key/value. Querystrings without a preset key will throw an error. 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ImageflowMiddlewareOptions SetUsePresetsExclusively(bool value)
        {
            UsePresetsExclusively = value;
            return this;
        }


        public ImageflowMiddlewareOptions MapPath(string virtualPath, string physicalPath)
            => MapPath(virtualPath, physicalPath, false);
        public ImageflowMiddlewareOptions MapPath(string virtualPath, string physicalPath, bool ignorePrefixCase)
        {
            mappedPaths.Add(new PathMapping(virtualPath, physicalPath, ignorePrefixCase));
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
        
        public ImageflowMiddlewareOptions SetAllowSqliteCaching(bool value)
        {
            this.AllowSqliteCaching = value;
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