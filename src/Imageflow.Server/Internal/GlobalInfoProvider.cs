using System.Reflection;
using Imazen.Abstractions.DependencyInjection;
using Imazen.Common.Helpers;
using Imazen.Common.Instrumentation.Support;
using Imazen.Common.Instrumentation.Support.InfoAccumulators;
using Imazen.Routing.Helpers;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Serving;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Imageflow.Server
{
    internal class GlobalInfoProvider(IImageServerContainer serviceProvider): IInfoProvider
    {
        
        private string? iisVersion;
        internal void CopyHttpContextInfo<T>(T request) where T : IHttpRequestStreamAdapter
        {
            iisVersion ??= request.TryGetServerVariable("SERVER_SOFTWARE");
        }
        
        private static string GetNetCoreVersion()
        {
            return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        }

        public void Add(IInfoAccumulator query)
        {
            var env = serviceProvider.GetService<IWebHostEnvironment>();

            var everything = serviceProvider.GetInstanceOfEverythingLocal<object>().ToList();
            var registeredObjectNames = everything
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
                
            var q = query.WithPrefix("proc_");
            if (iisVersion != null) 
                q.Add("iis", iisVersion);

            
            var thisAssembly = Assembly.GetAssembly(this.GetType());
            if (thisAssembly != null)
            {
                string? gitCommit = Imazen.Routing.Helpers.AssemblyHelpers.GetCommit(thisAssembly);
                var a = thisAssembly.GetInformationalVersion();
                gitCommit ??= a?.LastIndexOf('+') >= 0 ? a?[(a.LastIndexOf('+') + 1)..] : null;
                
                q.Add("git_commit", gitCommit);
                q.Add("info_version", thisAssembly.GetInformationalVersion());
                q.Add("file_version", thisAssembly.GetFileVersion());
            }


            if (env?.ContentRootPath != null)
            {
                // ReSharper disable once StringLiteralTypo
                q.Add("apppath_hash", Sha256TruncatedBase64(env.ContentRootPath, 6));
            }

            query.Add("imageflow",1);
            // query.AddString("enabled_cache", options.ActiveCacheBackend.ToString());
            // if (streamCache != null) query.AddString("stream_cache", streamCache.GetType().Name);
            
            
            foreach (var s in registeredObjectNames)
            {
                query.Add("p",s);
            }
            foreach (var p in everything.OfType<IInfoProvider>())
            {
                if (p != this)
                    p?.Add(query);
            }

        }
        
        private static string Sha256TruncatedBase64(string input, int bytes)
        {
            var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return EncodingUtils.ToBase64U(hash.Take(bytes).ToArray());
        }
    }
}