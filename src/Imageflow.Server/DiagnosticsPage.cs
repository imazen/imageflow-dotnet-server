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
using Imageflow.Server.Extensibility.ClassicDiskCache;
using Imazen.Common.Issues;
using Imazen.Common.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server
{
    internal class DiagnosticsPage
    {
        private readonly IWebHostEnvironment env;
        private ILogger<ImageflowMiddleware> logger;
        private readonly IMemoryCache memoryCache;
        private readonly IDistributedCache distributedCache;
        private readonly IClassicDiskCache diskCache;
        private readonly IList<IBlobProvider> blobProviders;
        
        internal DiagnosticsPage(IWebHostEnvironment env, ILogger<ImageflowMiddleware> logger, IMemoryCache memoryCache, IDistributedCache distributedCache,
            IClassicDiskCache diskCache, IList<IBlobProvider> blobProviders)
        {
            this.env = env;
            this.logger = logger;
            this.memoryCache = memoryCache;
            this.distributedCache = distributedCache;
            this.diskCache = diskCache;
            this.blobProviders = blobProviders;
        }

        public bool MatchesPath(string path) => "/imageflow.debug".Equals(path, StringComparison.Ordinal);
        
        public async Task Invoke(HttpContext context)
        {
            var s = "Imageflow Diagnostics Page only available in development";
            if (env.IsDevelopment())
            {
                s = await GeneratePage(context);
            }

            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.Headers.Add("X-Robots-Tag", "none");
            var bytes = Encoding.UTF8.GetBytes(s);
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        private Task<string> GeneratePage(HttpContext context)
        {
            var s = new StringBuilder(8096);
            var now = DateTime.UtcNow.ToString(NumberFormatInfo.InvariantInfo);
            s.AppendLine($"Diagnostics for Imageflow at {context?.Request.Host.Value} generated {now} UTC");
            
            try
            {
                using var job = new Imageflow.Bindings.JobContext();
                var version = job.GetVersionInfo();
                s.AppendLine($"libimageflow {version.LongVersionString}");
            }
            catch (Exception e)
            {
                s.AppendLine($"Failed to get libimageflow version: {e.Message}");
            }
            
            s.AppendLine("Please remember to provide this page when contacting support.");
            var issues = diskCache?.GetIssues().ToList() ?? new List<IIssue>();
            s.AppendLine($"{issues.Count} issues detected:\r\n");
            foreach (var i in issues.OrderBy(i => i?.Severity))
                s.AppendLine($"{i?.Source}({i?.Severity}):\t{i?.Summary}\n\t\t\t{i?.Details?.Replace("\n", "\r\n\t\t\t")}\n");

            s.AppendLine("\nInstalled Plugins");

            if (memoryCache != null) s.AppendLine(this.memoryCache.GetType().FullName);
            if (distributedCache != null) s.AppendLine(this.distributedCache.GetType().FullName);
            if (diskCache != null) s.AppendLine(this.diskCache.GetType().FullName);
            foreach (var provider in blobProviders)
            {
                s.AppendLine(provider.GetType().FullName);
            }


            s.AppendLine("\nAccepted querystring keys:\n");
            s.AppendLine(string.Join(", ", PathHelpers.SupportedQuerystringKeys));

            s.AppendLine("\nAccepted file extensions:\n");
            s.AppendLine(string.Join(", ", PathHelpers.AcceptedImageExtensions));

            s.AppendLine("\nEnvironment information:\n");
            s.AppendLine(
                $"Running on {Environment.OSVersion} and CLR {Environment.Version} and .NET Core {GetNetCoreVersion()}");
            
            try {
                var wow64 = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
                var arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
                s.AppendLine("OS arch: " + arch + (string.IsNullOrEmpty(wow64)
                    ? ""
                    : " !! Warning, running as 32-bit on a 64-bit OS(" + wow64 +
                      "). This will limit ram usage !!"));
            } catch (SecurityException) {
                s.AppendLine(
                    "Failed to detect operating system architecture - security restrictions prevent reading environment variables");
            }
            //Get loaded assemblies for later use
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            //List loaded assemblies, and also detect plugin assemblies that are not being used.
            s.AppendLine("\nLoaded assemblies:\n");
            
            foreach (var a in assemblies) {
                var assemblyName = new AssemblyName(a.FullName);
                var line = "";
                var error = a.GetExceptionForReading<AssemblyFileVersionAttribute>();
                if (error != null) {
                    line += $"{assemblyName.Name,-40} Failed to read assembly attributes: {error.Message}";
                } else {
                    var version = $"{a.GetFileVersion()} ({assemblyName.Version})";
                    var infoVersion = $"{a.GetInformationalVersion()}";
                    line += $"{assemblyName.Name,-40} File: {version,-25} Informational: {infoVersion,-30}";
                }
                
                s.AppendLine(line);
            }
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

    internal static class AssemblyExtensions
    {
        public static string IntoString(this IEnumerable<char> c) => string.Concat(c);

        private static T GetFirstAttribute<T>(this ICustomAttributeProvider a)
        {
            try
            {
                var attrs = a.GetCustomAttributes(typeof(T), false);
                if (attrs.Length > 0) return (T)attrs[0];
            }
            catch(FileNotFoundException) {
                //Missing dependencies
            }
            catch (Exception) { }
            return default(T);
        }

        public static Exception GetExceptionForReading<T>(this Assembly a)
        {
            try {
                var nah = a.GetCustomAttributes(typeof(T), false);
            } catch (Exception e) {
                return e;
            }
            return null;
        }
        
        public static string GetInformationalVersion(this Assembly a)
        {
            return GetFirstAttribute<AssemblyInformationalVersionAttribute>(a)?.InformationalVersion;
        }
        public static string GetFileVersion(this Assembly a)
        {
            return GetFirstAttribute<AssemblyFileVersionAttribute>(a)?.Version;
        }
    }
}