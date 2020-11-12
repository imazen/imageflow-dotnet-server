using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imageflow.Server.Extensibility;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.Common.Instrumentation.Support;
using Imazen.Common.Instrumentation.Support.InfoAccumulators;
using Imazen.Common.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server
{
    internal class GlobalInfoProvider: IInfoProvider
    {
        private readonly IWebHostEnvironment env;
        private readonly ImageflowMiddlewareOptions options;
        private readonly List<string> pluginNames;
        public GlobalInfoProvider(ImageflowMiddlewareOptions options,IWebHostEnvironment env, ILogger<ImageflowMiddleware> logger,  ISqliteCache sqliteCache,  IMemoryCache memoryCache, IDistributedCache distributedCache,
            IClassicDiskCache diskCache, IList<IBlobProvider> blobProviders)
        {
            this.env = env;
            this.options = options;
            var plugins = new List<object>(){logger, memoryCache, diskCache, distributedCache}.Concat(blobProviders);
            ;
            pluginNames = plugins
                .Where(p => p != null)
                .Select(p =>
            {
                var t = p.GetType();
                if (t.Namespace != null && 
                    (t.Namespace.StartsWith("Imazen") ||
                    t.Namespace.StartsWith("Imageflow") ||
                    t.Namespace.StartsWith("Microsoft.Extensions.Logging") ||
                    t.Namespace.StartsWith("Microsoft.Extensions.Caching")))
                {
                    return t.Name;
                }
                else
                {
                    return t.FullName;
                }

            }).ToList();
            
 

        }

        private string iisVersion;

        internal void CopyHttpContextInfo(HttpContext context)
        {
            if (iisVersion == null)
            {
                var serverVars = context.Features.Get<IServerVariablesFeature>();
                iisVersion = serverVars?["SERVER_SOFTWARE"];
            }
            
        }
        
        private static string GetNetCoreVersion()
        {
            var assembly = typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly;
            var assemblyPath = assembly.CodeBase?.Split(new[] {'/', '\\'}, StringSplitOptions.RemoveEmptyEntries) ??
                               new string[0];
            var netCoreAppIndex = Array.IndexOf(assemblyPath, "Microsoft.NETCore.App");
            if (netCoreAppIndex > 0 && netCoreAppIndex < assemblyPath.Length - 2)
                return assemblyPath[netCoreAppIndex + 1];
            return null;
        }

        public void Add(IInfoAccumulator query)
        {
            var q = query.WithPrefix("proc_");
            if (iisVersion != null) 
                q.Add("iis", iisVersion);

            q.Add("default_commands", PathHelpers.SerializeCommandString(options.CommandDefaults));

            var a = Assembly.GetAssembly(this.GetType()).GetInformationalVersion();
            if (a.LastIndexOf('+') >= 0)
            {
                q.Add("git_commit", a.Substring(a.LastIndexOf('+') + 1));
            }
            q.Add("info_version", Assembly.GetAssembly(this.GetType()).GetInformationalVersion());
            q.Add("file_version", Assembly.GetAssembly(this.GetType()).GetFileVersion());


            if (env.ContentRootPath != null)
            {
                // ReSharper disable once StringLiteralTypo
                q.Add("apppath_hash", Utilities.Sha256TruncatedBase64(env.ContentRootPath, 6));
            }

            query.Add("imageflow",1);
            query.AddString("enabled_cache", options.ActiveCacheBackend.ToString());
            query.Add("map_web_root", options.MapWebRoot);
            query.Add("use_presets_exclusively", options.UsePresetsExclusively);
            query.Add("request_signing_default", options.RequestSignatureOptions?.DefaultRequirement.ToString() ?? "never");
            query.Add("default_cache_control", options.DefaultCacheControlString);
            
            foreach (var s in pluginNames)
            {
                query.Add("p",s);
            }
            
            /*
             *
             * sqlite/memory/distributed/disk cache enabled
             * 
             *
                     provider_flags 1,1,0,0,1,1,1
                    provider_prefix /s3/
              diskcache_virtualpath /imagecache
              diskcache_drive_total 161059172352
              diskcache_drive_avail 38921302016
               diskcache_filesystem NTFS
            diskcache_network_drive 0
               diskcache_subfolders 8192
              diskcache_asyncwrites 0
                diskcache_autoclean 0
             */
        }
    }
}