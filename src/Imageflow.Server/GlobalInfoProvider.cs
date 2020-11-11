using System.Collections.Generic;
using System.Linq;
using Imazen.Common.Instrumentation.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Imageflow.Server
{
    internal class GlobalInfoProvider: IInfoProvider
    {
        private IWebHostEnvironment env;
        private ImageflowMiddlewareOptions options;
        private List<string> pluginNames;
        public GlobalInfoProvider(IWebHostEnvironment env, ImageflowMiddlewareOptions options, IEnumerable<object> plugins)
        {
            this.env = env;
            this.options = options;
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

        public void Add(IInfoAccumulator query)
        {
            var q = query.WithPrefix("proc_");
            //q.Add("sys_dotnet", DotNetVersionInstalled);
            //q.Add("iis", IisVer);
            q.Add("default_commands", PathHelpers.SerializeCommandString(options.CommandDefaults));
            //q.Add("git_commit", Assembly.GetAssembly(this.GetType()).GetShortCommit());
            
            if (env.ContentRootPath != null)
            {
                q.Add("apppath_hash", Utilities.Sha256TruncatedBase64(env.ContentRootPath, 6));
            }

            foreach (var s in pluginNames)
            {
                query.Add("p",s);
            }
        }
    }
}