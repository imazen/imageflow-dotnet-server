/* Copyright (c) 2014 Imazen See license.txt for your rights. */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.Common.Issues;
using Microsoft.Extensions.Logging;

namespace Imazen.DiskCache {

    internal static class TaskExtensions
    {
        // Allocate the async/await state machine only when needed for performance reason.
        // More info about the state machine: https://blogs.msdn.microsoft.com/seteplia/2017/11/30/dissecting-the-async-methods-in-c/
        static async Task ForgetAwaited(Task task)
        {
            try
            {
                // No need to resume on the original SynchronizationContext, so use ConfigureAwait(false)
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Nothing to do here
            }
        }
        public static void Forget(this Task task)
        {
            // note: this code is inspired by a tweet from Ben Adams: https://twitter.com/ben_a_adams/status/1045060828700037125
            // Only care about tasks that may fault (not completed) or are faulted,
            // so fast-path for SuccessfullyCompleted and Canceled tasks.
            if (!task.IsCompleted || task.IsFaulted)
            {
                // use "_" (Discard operation) to remove the warning IDE0058: Because this call is not awaited, execution of the current method continues before the call is completed
                // https://docs.microsoft.com/en-us/dotnet/csharp/discards#a-standalone-discard
                _ = ForgetAwaited(task);
            }
        }
    }
    
    internal class CleanupManager:IIssueProvider, IDisposable {
        private readonly ICleanableCache cache = null;
        private readonly CleanupStrategy cs = null;
        private readonly CleanupQueue queue = null;
        private readonly CleanupWorker worker = null;

        private readonly ILogger logger = null;
        public CleanupManager(ILogger logger, ICleanableCache cache, CleanupStrategy cs) {
            this.cache = cache;
            this.cs = cs;
            this.logger = logger;
            queue = new CleanupQueue();
            //Called each request
            cache.CacheResultReturned += delegate(ICleanableCache sender, CacheResult r) {
                if (r.Result == CacheQueryResult.Miss)
                    this.AddedFile(r.RelativePath); //It was either updated or added.
                else
                    this.BeLazy();
            };
            //Called when the file system changes unexpectedly.
            cache.Index.FileDisappeared += delegate(string relativePath, string physicalPath) {
                logger?.LogWarning("File disappeared from the cache unexpectedly - reindexing entire cache. File name: {0}", relativePath);
                //Stop everything ASAP and start a brand new cleaning run.
                queue.ReplaceWith(new CleanupWorkItem(CleanupWorkItem.Kind.CleanFolderRecursive, "", cache.PhysicalCachePath));
                worker.MayHaveWork();
            };

            worker = new CleanupWorker(this.logger, cs,queue,cache);
        }



        /// <summary>
        /// When true, indicates that another process is managing cleanup operations - this thread is idle, waiting for the other process to end before it can pick up work.
        /// </summary>
        public bool ExternalProcessCleaning => worker?.ExternalProcessCleaning ?? false;

        /// <summary>
        /// Notifies the CleanupManager that a request is in process. Helps CleanupManager optimize background work so it doesn't interfere with request processing.
        /// </summary>
        public void BeLazy() {
            worker.BeLazy();
        }
        /// <summary>
        /// Notifies the CleanupManager that a file was added under the specified relative path. Allows CleanupManager to detect when a folder needs cleanup work.
        /// </summary>
        /// <param name="relativePath"></param>
        public void AddedFile(string relativePath) {

            //TODO: Maybe we shouldn't queue a task to compare the numbers every time a file is added? 

            int slash = relativePath.LastIndexOf('/');
            string folder = slash > -1 ? relativePath.Substring(0, slash) : "";
            char c = System.IO.Path.DirectorySeparatorChar;
            string physicalFolder =  cache.PhysicalCachePath.TrimEnd(c) + c + folder.Replace('/',c).Replace('\\',c).Trim(c);

            //Only queue the item if it doesn't already exist.
            if (queue.QueueIfUnique(new CleanupWorkItem(CleanupWorkItem.Kind.CleanFolderRecursive, folder,physicalFolder)))
                worker.MayHaveWork();
        }

        public void CleanAll() {
            logger?.LogDebug("Queuing CleanAll() task");
            //Only queue the item if it doesn't already exist.
            if (queue.QueueIfUnique(new CleanupWorkItem(CleanupWorkItem.Kind.CleanFolderRecursive, "", cache.PhysicalCachePath)))
                worker.MayHaveWork();
        }

        public void UsedFile(string relativePath, string physicalPath) {
            //Bump the date in memory
            cache.Index.bumpDateIfExists(relativePath);
            
            Task.Run(async () =>
            {
                // Wait 10 seconds to give the reader thread time to open the file first. 
                await Task.Delay(10000);
                //Make sure the 'flush' job for the file is in the queue somewhere, so the access date will get written to disk.
                queue.QueueIfUnique(new CleanupWorkItem(CleanupWorkItem.Kind.FlushAccessedDate, relativePath,
                    physicalPath));
                //In case it's paused
                worker.MayHaveWork();
            }).Forget();
        }


        public void Dispose() {
            worker.Dispose();
        }

        public IEnumerable<IIssue> GetIssues() {
            var issues = new List<IIssue>();
            if (worker != null) issues.AddRange(worker.GetIssues());
            if (cs != null) issues.AddRange(cs.GetIssues());
            return issues;
        }


    }
   
}
