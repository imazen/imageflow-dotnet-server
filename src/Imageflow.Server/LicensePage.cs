using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.Common.Issues;
using Imazen.Common.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Imageflow.Server
{
    internal class LicensePage
    {
        private readonly ImageflowMiddlewareOptions options;
        
        internal LicensePage(ImageflowMiddlewareOptions options)
        {
            this.options = options;
        }

        public bool MatchesPath(string path) => "/imageflow.license".Equals(path, StringComparison.Ordinal);

        public async Task Invoke(HttpContext context)
        {

            var s = await GeneratePage(context);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.Headers.Add("X-Robots-Tag", "none");
            context.Response.Headers[HeaderNames.CacheControl] = "no-store";
            var bytes = Encoding.UTF8.GetBytes(s);
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        private Task<string> GeneratePage(HttpContext context)
        {
            var s = new StringBuilder(8096);
            var now = DateTime.UtcNow.ToString(NumberFormatInfo.InvariantInfo);
            s.AppendLine($"License page for Imageflow at {context?.Request.Host.Value} generated {now} UTC");

            s.Append(options.Licensing.GetLicensePageContents());
            return Task.FromResult(s.ToString());
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


    }

}