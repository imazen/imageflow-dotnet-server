using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.Common.Issues;
using Microsoft.Extensions.Logging;

namespace Imazen.DiskCache
{
    public class ClassicDiskCache
    {
        private readonly ClassicDiskCacheSettings settings;
        public ClassicDiskCache(ClassicDiskCacheSettings settings, ILogger log)
        {
            this.Logger = log;
            this.settings = settings; 
            this.settings.MakeImmutable();
        }

        private ILogger Logger { get; } = null;
        private AsyncCustomDiskCache cache = null;
        private CleanupManager cleaner = null;

        /// <summary>
        /// Returns true if the configured settings are valid and .NET (not NTFS) permissions will work.
        /// </summary>
        /// <returns></returns>
        private bool IsConfigurationValid()
        {
            return !string.IsNullOrEmpty(settings.PhysicalCacheDir) && settings.Enabled;
        }
        
        private readonly object startSync = new object();
        private volatile bool started = false;
        /// <summary>
        /// Returns true if the DiskCache instance is operational.
        /// </summary>
        private bool Started => started;

        /// <summary>
        /// Attempts to start the DiskCache using the current settings. 
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken) {
            
            if (!IsConfigurationValid())     throw new InvalidOperationException("DiskCache configuration invalid");
        
            lock (startSync) {
                if (started) return Task.CompletedTask;
                if (!IsConfigurationValid())  throw new InvalidOperationException("DiskCache configuration invalid");

                cache = new AsyncCustomDiskCache(Logger, settings.PhysicalCacheDir, settings.Subfolders, settings.AsyncBufferSize);
                //Init the cleanup worker
                if (settings.AutoClean) cleaner = new CleanupManager(Logger, (ICleanableCache)cache, settings.CleanupStrategy);
                //If we're running with subfolders, enqueue the cache root for cleanup (after the 5 minute delay)
                //so we don't eternally 'skip' files in the root or in other unused subfolders (since only 'accessed' subfolders are ever cleaned ). 
                cleaner?.CleanAll();

                Logger?.LogInformation("DiskCache started successfully.");
                //Started successfully
                started = true;
                return Task.CompletedTask;
            }
        }
        /// <summary>
        ///    Cannot be restarted once stopped.
        /// </summary>
        /// <returns></returns>
        public Task StopAsync(CancellationToken cancellationToken) {
            cleaner?.Dispose();
            cleaner = null;
            return Task.CompletedTask;
        }
        
        public async Task<ICacheResult> GetOrCreate(string key, string fileExtension, AsyncWriteResult writeCallback)
        {
            //Cache the data to disk and return a path.
            var r = await cache.GetCachedFile(key, fileExtension, writeCallback, settings.CacheAccessTimeout, settings.AsyncWrites);
            
            if (r.Result == CacheQueryResult.Hit)
                cleaner?.UsedFile(r.RelativePath, r.PhysicalPath);
            
            return r;
        }

        private bool HasNTFSPermission(){
            try {
                if (!Directory.Exists(settings.PhysicalCacheDir)) Directory.CreateDirectory(settings.PhysicalCacheDir);
                var testFile = Path.Combine(settings.PhysicalCacheDir, "TestFile.txt");
                File.WriteAllText(testFile, "You may delete this file - it is written and deleted just to verify permissions are configured correctly");
                File.Delete(testFile);
                return true;
            } catch (Exception){
                return false;
            }
        }

        private string GetExecutingUser() {
            try {
                return Thread.CurrentPrincipal.Identity.Name;
            } catch {
                return "[Unknown - please check App Pool configuration]";
            }
        }

        private bool CacheDriveOnNetwork()
        {
            string physicalCache = settings.PhysicalCacheDir;
            if (!string.IsNullOrEmpty(physicalCache))
            {
                return physicalCache.StartsWith("\\\\") || GetCacheDrive()?.DriveType == DriveType.Network;
            }
            return false;
        }

        private DriveInfo GetCacheDrive()
        {
            try
            {
                var drive = string.IsNullOrEmpty(settings.PhysicalCacheDir) ? null : new DriveInfo(Path.GetPathRoot(settings.PhysicalCacheDir));
                return (drive?.IsReady == true) ? drive : null;
            }
            catch { return null; }
        }

        
        

        
         public IEnumerable<IIssue> GetIssues() {
            var issues = new List<IIssue>();
            if (cleaner != null) issues.AddRange(cleaner.GetIssues());


            if (!HasNTFSPermission()) 
                issues.Add(new Issue("DiskCache", "Not working: Your NTFS Security permissions are preventing the application from writing to the disk cache",
    "Please give user " + GetExecutingUser() + " read and write access to directory \"" + settings.PhysicalCacheDir + "\" to correct the problem. You can access NTFS security settings by right-clicking the aforementioned folder and choosing Properties, then Security.", IssueSeverity.ConfigurationError));

            if (!Started && !settings.Enabled) issues.Add(new Issue("DiskCache", "DiskCache has been disabled by DiskCache settings.", null, IssueSeverity.ConfigurationError));

            //Warn user about setting hashModifiedDate=false in a web garden.
            if (settings.AsyncBufferSize < 1024 * 1024 * 2)
                issues.Add(new Issue("DiskCache", "The asyncBufferSize should not be set below 2 megabytes (2097152). Found in the <diskcache /> element in Web.config.",
                    "A buffer that is too small will cause requests to be processed synchronously. Remember to set the value to at least 4x the maximum size of an output image.", IssueSeverity.ConfigurationError));


            if (CacheDriveOnNetwork())
                issues.Add(new Issue("DiskCache", "It appears that the cache directory is located on a network drive.",
                    "Both IIS and ASP.NET have trouble hosting websites with large numbers of folders over a network drive, such as a SAN. The cache will create " +
                    settings.Subfolders.ToString() + " subfolders. If the total number of network-hosted folders exceeds 100, you should contact support@imageresizing.net and consult the documentation for details on configuring IIS and ASP.NET for this situation.", IssueSeverity.Warning));
                    
            return issues;
        }
    }
}