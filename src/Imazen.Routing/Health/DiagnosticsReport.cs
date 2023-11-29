using System.Globalization;
using System.Reflection;
using System.Security;
using System.Text;
using Imazen.Abstractions;
using Imazen.Abstractions.BlobCache;
using Imazen.Abstractions.Blobs.LegacyProviders;
using Imazen.Abstractions.DependencyInjection;
using Imazen.Abstractions.Logging;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Issues;
using Imazen.Common.Storage;
using Imazen.Routing.Helpers;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Serving;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imazen.Routing.Health;

public class StartupDiagnostics(IImageServerContainer serviceProvider)
{
    public void LogIssues(IReLogger logger)
    {
        DetectNamingConflicts(logger, false);
        // Log an error warning if IStreamCache or IClassicDiskCache has any registrations
        // Each implementation gets its own error so users know what class & assembly to search for
#pragma warning disable CS0618 // Type or member is obsolete
        var classicDiskCaches = serviceProvider.GetService<IEnumerable<IClassicDiskCache>>();
#pragma warning restore CS0618 // Type or member is obsolete
        if (classicDiskCaches != null)
        {
            foreach (var cache in classicDiskCaches)
            {
                logger?.WithRetain.LogError(
                    "IClassicDiskCache is obsolete and ignored. Please use the official caches or implement IBlobCache instead. {FullName} is registered in {Assembly}",
                    cache.GetType().FullName, cache.GetType().Assembly.FullName);
            }
        }
        
#pragma warning disable CS0618 // Type or member is obsolete
        var streamCaches = serviceProvider.GetService<IEnumerable<IStreamCache>>();
#pragma warning restore CS0618 // Type or member is obsolete
        if (streamCaches == null) return;
        foreach (var cache in streamCaches)
        {
            logger?.WithRetain.LogError(
                "IStreamCache is obsolete and ignored. Please use the official caches or implement IBlobCache instead. {FullName} is registered in {Assembly}",
                cache.GetType().FullName, cache.GetType().Assembly.FullName);
        }
        
    }

    private void DetectNamingConflicts(IReLogger logger, bool throwOnConflict = true)
    {
        var blobCacheProviders = serviceProvider.GetService<IEnumerable<IBlobCacheProvider>>();
        var all = new List<IUniqueNamed>();
        if (blobCacheProviders != null)
        {
            foreach (var provider in blobCacheProviders)
            {
                var caches = provider.GetBlobCaches();
                all.AddRange(caches);
            }
        }
        all.AddRange(serviceProvider.GetInstanceOfEverythingLocal<IUniqueNamed>());
        // deduplicate instances by reference
        all = all.Distinct().ToList();
        
        // throw exception if any duplicate names exist, and put the type names in the exception message
        var duplicateNames = all.GroupBy(x => x.UniqueName).Where(x => x.Count() > 1)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            StringBuilder sb = new StringBuilder();
            // collect all the type names
            // for each group of conflicting names, log an error
            foreach (var item in duplicateNames.SelectMany(group => group))
            {
                logger?.WithRetain.LogError(
                    "Duplicate IUniqueName '{UniqueName}' detected (assigned to type {FullName}, from {Assembly})",
                    item.UniqueName, item.GetType().FullName, item.GetType().Assembly.FullName);
                sb.AppendLine($"Duplicate IUniqueName '{item.UniqueName}' detected (assigned to type {item.GetType().FullName}, from {item.GetType().Assembly.FullName})");
            }
            if (throwOnConflict)
            {
                throw new InvalidOperationException(sb.ToString());
            }
        }

    }

    public void Validate(IReLogger logger)
    {
        DetectNamingConflicts(logger, true);
    }
}

public class DiagnosticsReport(IServiceProvider serviceProvider, IReLogStore logStore)
{

    /// <summary>
    /// SectionProviders should include Licensing and ImageServer at minimum
    /// </summary>
    /// <param name="request"></param>
    /// <param name="sectionProviders"></param>
    /// <returns></returns>
    public ValueTask<string> GetReport(IHttpRequestStreamAdapter? request, IReadOnlyCollection<IHasDiagnosticPageSection> sectionProviders)
    {
        // TODO: add support for beginning tasks and having a refresh header that reloads the page in time for those health
        // checks to complete
        
        var sections =
            GetAllImplementers<IHasDiagnosticPageSection>(serviceProvider).Concat(sectionProviders).Distinct().ToList();
        
            
        var s = new StringBuilder(8096);
        var now = DateTime.UtcNow.ToString(NumberFormatInfo.InvariantInfo);
        s.AppendLine($"Diagnostics for Imageflow at {request?.GetHost().Value} generated {now} UTC");

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
        
        
        // get array of loaded assemblies
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var summary = assemblies.ExplainVersionMismatches(true);
        if (!string.IsNullOrEmpty(summary))
        {
            s.AppendLine(summary);
        }


        var issueProviders = GetAllImplementers<IIssueProvider>(serviceProvider).ToList();
        if (issueProviders.Count == 0)
        {
            s.AppendLine("No IIssueProvider implementations detected.");
        }
        else
        {
            var issues = issueProviders
                .SelectMany(p => p.GetIssues()).ToList();
            s.AppendLine(
                $"{issues.Count} issues from {issueProviders.Count} legacy plugins (those implementing IIssueProvider instead of calling IReLogger.WithRetain.) detected:\r\n");
            foreach (var i in issues.OrderBy(i => i?.Severity))
                s.AppendLine(
                    $"{i?.Source}({i?.Severity}):\t{i?.Summary}\n\t\t\t{i?.Details?.Replace("\n", "\r\n\t\t\t")}\n");
        }
        
        foreach (var sectionProvider in sections)
        {
            s.AppendLine(sectionProvider.GetDiagnosticsPageSection(DiagnosticsPageArea.Start));
        }
        
        // ReStore report
        var shortReport = logStore.GetReport(new ReLogStoreReportOptions()
        {
            ReportType = ReLogStoreReportType.FullReport,
        });
        s.AppendLine(shortReport);
        
        s.AppendLine("\nAccepted querystring keys:\n");
        s.AppendLine(string.Join(", ", PathHelpers.SupportedQuerystringKeys));

        s.AppendLine("\nAccepted file extensions:\n");
        s.AppendLine(string.Join(", ", PathHelpers.AcceptedImageExtensions));

        s.AppendLine("\nEnvironment information:\n");
        s.AppendLine(
            $"Running on {Environment.OSVersion} and CLR {Environment.Version} and .NET Core {GetNetCoreVersion()}");

        try
        {
            var wow64 = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
            var arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            s.AppendLine("OS arch: " + arch + (string.IsNullOrEmpty(wow64)
                ? ""
                : " !! Warning, running as 32-bit on a 64-bit OS(" + wow64 +
                  "). This will limit ram usage !!"));
        }
        catch (SecurityException)
        {
            s.AppendLine(
                "Failed to detect operating system architecture - security restrictions prevent reading environment variables");
        }

        
        //List loaded assemblies, and also detect plugin assemblies that are not being used.
        s.AppendLine("\nLoaded assemblies:\n");

        foreach (var a in assemblies)
        {
            if (a.FullName == null) continue;
            var assemblyName = new AssemblyName(a.FullName);
            var line = "";
            var error = a.GetExceptionForReading<AssemblyFileVersionAttribute>();
            if (error != null)
            {
                line += $"{assemblyName.Name,-40} Failed to read assembly attributes: {error.Message}";
            }
            else
            {
                var version = $"{a.GetFileVersion()} ({assemblyName.Version})";
                var infoVersion = $"{a.GetInformationalVersion()}";
                line += $"{assemblyName.Name,-40} File: {version,-25} Informational: {infoVersion,-30}";
            }

            s.AppendLine(line);
        }
        
        // Now get the sections while passing in DiagnosticsPageArea.End
        
        foreach (var sectionProvider in sections)
        {
            var section = sectionProvider.GetDiagnosticsPageSection(DiagnosticsPageArea.End);
            if (!string.IsNullOrEmpty(section))
            {
                s.AppendLine(section);
            }
        }
        
        return Tasks.ValueResult(s.ToString());
    }

    private static List<T> GetAllImplementers<T>(IServiceProvider serviceProvider) where T: class
    {
        void Add<TV>(ICollection<T> candidates) 
        {
            var items = serviceProvider.GetService<IEnumerable<TV>>();
            if (items == null) return;
            foreach (var tvObj in items)
            {
                if (tvObj is not T t) continue;
                if (!candidates.Contains(t))
                {
                    candidates.Add(t);
                }
            }
        }
        var c = new List<T>();
        Add<IIssueProvider>(c);
        Add<IBlobWrapperProvider>(c);
        Add<IBlobCache>(c);
        Add<IBlobCacheProvider>(c);
        
#pragma warning disable CS0618 // Type or member is obsolete
        Add<IBlobProvider>(c);
        Add<IClassicDiskCache>(c);
        Add<IStreamCache>(c);
        Add<IHasDiagnosticPageSection>(c);
#pragma warning restore CS0618 // Type or member is obsolete
        return c.Distinct().ToList();
    }
    private static string GetNetCoreVersion()
    {
        return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
    }
}




