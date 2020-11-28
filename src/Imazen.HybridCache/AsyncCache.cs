using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Concurrency;
using Imazen.Common.Extensibility.StreamCache;
using Microsoft.Extensions.Logging;

namespace Imazen.HybridCache
{
    internal class AsyncCache
    {
        public enum AsyncCacheDetailResult
        {
            Unknown = 0,
            MemoryHit,
            DiskHit,
            WriteSucceeded,
            QueueLockTimeoutAndCreated,
            FileAlreadyExists,
            Miss,
            CacheEvictionFailed,
            WriteTimedOut,
            QueueLockTimeoutAndFailed
        }
        public class AsyncCacheResult : IStreamCacheResult
        {
            public Stream Data { get; set; }
            public string ContentType { get; set; }
            public string Status => Detail.ToString();
            
            public AsyncCacheDetailResult Detail { get; set; }
        }
        
        public AsyncCache(AsyncCacheOptions options, ICacheCleanupManager cleanupManager,HashBasedPathBuilder pathBuilder, ILogger logger)
        {
            Options = options;
            PathBuilder = pathBuilder;
            CleanupManager = cleanupManager;
            Logger = logger;
            Locks = new AsyncLockProvider();
            QueueLocks = new AsyncLockProvider();
            CurrentWrites = new AsyncWriteCollection(options.MaxQueuedBytes);
            FileWriter = new CacheFileWriter(Locks);
        }

        
        private AsyncCacheOptions Options { get; }
        private HashBasedPathBuilder PathBuilder { get; }
        private ILogger Logger { get; }

        private ICacheCleanupManager CleanupManager { get; }
        
        /// <summary>
        /// Provides string-based locking for file write access.
        /// </summary>
        private AsyncLockProvider Locks {get; }
        
        private CacheFileWriter FileWriter { get; }

        /// <summary>
        /// Provides string-based locking for image resizing (not writing, just processing). Prevents duplication of efforts in asynchronous mode, where 'Locks' is not being used.
        /// </summary>
        private AsyncLockProvider QueueLocks { get;  }

        /// <summary>
        /// Contains all the queued and in-progress writes to the cache. 
        /// </summary>
        private AsyncWriteCollection CurrentWrites {get; }


        public Task AwaitEnqueuedTasks()
        {
            return CurrentWrites.AwaitAllCurrentTasks();
        }

        private async Task<AsyncCacheResult> TryGetFileBasedResult(CacheEntry entry, bool retrieveContentType, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var contentType = retrieveContentType
                ? await CleanupManager.GetContentType(entry, cancellationToken)
                : null;

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            try
            {
                return new AsyncCacheResult
                {
                    Detail = AsyncCacheDetailResult.DiskHit,
                    ContentType = contentType,
                    Data = new FileStream(entry.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                };
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
        
        /// <summary>
        /// Tries to fetch the result from disk cache, the memory queue, or create it. If the memory queue has space,
        /// the writeCallback() will be executed and the resulting bytes put in a queue for writing to disk.
        /// If the memory queue is full, writing to disk will be attempted synchronously.
        /// In either case, writing to disk can also fail if the disk cache is full and eviction fails.
        /// If the memory queue is full, eviction will be done synchronously and can cause other threads to time out
        /// while waiting for QueueLock
        /// </summary>
        /// <param name="key"></param>
        /// <param name="dataProviderCallback"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="retrieveContentType"></param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public async Task<AsyncCacheResult> GetOrCreateBytes(
            byte[] key,
            AsyncBytesResult dataProviderCallback,
            CancellationToken cancellationToken,
            bool retrieveContentType)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var swGetOrCreateBytes = Stopwatch.StartNew();
            var entry = new CacheEntry(key, PathBuilder);
            
            // Tell cleanup what we're using
            CleanupManager.NotifyUsed(entry);
            
            // Fast path on disk hit
            var swFileExists = Stopwatch.StartNew();
            if (File.Exists(entry.PhysicalPath))
            {
                var fileBasedResult = await TryGetFileBasedResult(entry, retrieveContentType, cancellationToken);
                if (fileBasedResult != null)
                {
                    return fileBasedResult;
                }
                // Just continue on creating the file. It must have been deleted between the calls
            }
            swFileExists.Stop();
            


            var cacheResult = new AsyncCacheResult();
            
            //Looks like a miss. Let's enter a lock for the creation of the file. This is a different locking system
            // than for writing to the file 
            //This prevents two identical requests from duplicating efforts. Different requests don't lock.

            //Lock execution using relativePath as the sync basis. Ignore casing differences. This prevents duplicate entries in the write queue and wasted CPU/RAM usage.
            var queueLockComplete = await QueueLocks.TryExecuteAsync(entry.StringKey,
                Options.WaitForIdenticalRequestsTimeoutMs, cancellationToken,
                async () =>
                {
                    var swInsideQueueLock = Stopwatch.StartNew();
                    
                    // Now, if the item we seek is in the queue, we have a memcached hit.
                    // If not, we should check the filesystem. It's possible the item has been written to disk already.
                    // If both are a miss, we should see if there is enough room in the write queue.
                    // If not, switch to in-thread writing. 

                    var existingQueuedWrite = CurrentWrites.Get(entry.StringKey);

                    if (existingQueuedWrite != null)
                    {
                        cacheResult.Data = existingQueuedWrite.GetReadonlyStream();
                        cacheResult.ContentType = existingQueuedWrite.ContentType;
                        cacheResult.Detail = AsyncCacheDetailResult.MemoryHit;
                        return;
                    }
                    
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException(cancellationToken);

                    swFileExists.Start();
                    // Fast path on disk hit, now that we're in a synchronized state
                    if (File.Exists(entry.PhysicalPath))
                    {
                        var fileBasedResult = await TryGetFileBasedResult(entry, retrieveContentType, cancellationToken);
                        if (fileBasedResult != null)
                        {
                            cacheResult = fileBasedResult;
                            return;
                        }
                        // Just continue on creating the file. It must have been deleted between the calls
                    }
                    swFileExists.Stop();

                    var swDataCreation = Stopwatch.StartNew();
                    //Read, resize, process, and encode the image. Lots of exceptions thrown here.
                    var result = await dataProviderCallback(cancellationToken);
                    swDataCreation.Stop();
                    
                    //Create AsyncWrite object to enqueue
                    var w = new AsyncWrite(entry.StringKey, result.Item2, result.Item1);

                    cacheResult.Detail = AsyncCacheDetailResult.Miss;
                    cacheResult.ContentType = w.ContentType;
                    cacheResult.Data = w.GetReadonlyStream();

                    // Create a lambda which we can call either in a spawned Task (if enqueued successfully), or
                    // in this task, if our buffer is full.
                    async Task<AsyncCacheDetailResult> EvictWriteAndLog(bool queueFull, TimeSpan dataCreationTime,  CancellationToken ct)
                    {
                        var delegateStartedAt = DateTime.UtcNow;
                        var swReserveSpace = Stopwatch.StartNew();
                        //We only permit eviction proceedings from within the queue or if the queue is disabled
                        var allowEviction = !queueFull || CurrentWrites.MaxQueueBytes <= 0;
                        var reserveSpaceResult = await CleanupManager.TryReserveSpace(entry, w.ContentType, 
                            w.GetUsedBytes(), allowEviction, ct);
                        swReserveSpace.Stop();

                        var syncString = queueFull ? "synchronous" : "async";
                        if (!reserveSpaceResult)
                        {
                            //We failed to lock the file.
                            Logger?.LogError("HybridCache {0} write failed; could not evict enough space from cache. Time taken: {1}ms - {2}", syncString, swReserveSpace.ElapsedMilliseconds, entry.DisplayPath);
                            return AsyncCacheDetailResult.CacheEvictionFailed;
                        }

                        var swIo = Stopwatch.StartNew();
                        // We only force an immediate File.Exists check when running from the Queue
                        // Otherwise it happens inside the lock
                        var fileWriteResult = await FileWriter.TryWriteFile(entry, delegate(Stream s, CancellationToken ct2)
                        {
                            if (ct2.IsCancellationRequested) throw new OperationCanceledException(ct2);

                            var fromStream = w.GetReadonlyStream();
                            return fromStream.CopyToAsync(s, 81920, ct2);
                        }, !queueFull, Options.WaitForIdenticalDiskWritesMs, ct);
                        swIo.Stop();

                        var swMarkCreated = Stopwatch.StartNew();
                        // Mark the file as created so it can be deleted
                        await CleanupManager.MarkFileCreated(entry);
                        swMarkCreated.Stop();
                        
                        switch (fileWriteResult.Status)
                        {
                            case CacheFileWriter.FileWriteStatus.LockTimeout:
                                //We failed to lock the file.
                                Logger?.LogWarning("HybridCache {Sync} write failed; disk lock timeout exceeded after {IoTime}ms - {DisplayPath}", 
                                    syncString, swIo.ElapsedMilliseconds, entry.DisplayPath);
                                return AsyncCacheDetailResult.WriteTimedOut;
                            case CacheFileWriter.FileWriteStatus.FileAlreadyExists:
                                Logger?.LogTrace("HybridCache {Sync} write found file already exists in {IoTime}ms, after a {DelayTime}ms delay and {CreationTime}- {DisplayPath}", 
                                    syncString, swIo.ElapsedMilliseconds, 
                                    delegateStartedAt.Subtract(w.JobCreatedAt).TotalMilliseconds, 
                                    dataCreationTime, entry.DisplayPath);
                                return AsyncCacheDetailResult.FileAlreadyExists;
                            case CacheFileWriter.FileWriteStatus.FileCreated:
                                if (queueFull)
                                {
                                    Logger?.LogTrace(@"HybridCache synchronous write complete. Create: {CreateTime}ms. Write {WriteTime}ms. Mark Created: {MarkCreatedTime}ms. Eviction: {EvictionTime}ms - {DisplayPath}", 
                                        Math.Round(dataCreationTime.TotalMilliseconds).ToString(CultureInfo.InvariantCulture).PadLeft(4),
                                        swIo.ElapsedMilliseconds.ToString().PadLeft(4),
                                        swMarkCreated.ElapsedMilliseconds.ToString().PadLeft(4), 
                                        swReserveSpace.ElapsedMilliseconds.ToString().PadLeft(4), entry.DisplayPath);
                                }
                                else
                                {
                                    Logger?.LogTrace(@"HybridCache async write complete. Create: {CreateTime}ms. Write {WriteTime}ms. Mark Created: {MarkCreatedTime}ms Eviction {EvictionTime}ms. Delay {DelayTime}ms. - {DisplayPath}", 
                                        Math.Round(dataCreationTime.TotalMilliseconds).ToString(CultureInfo.InvariantCulture).PadLeft(4),
                                        swIo.ElapsedMilliseconds.ToString().PadLeft(4), 
                                        swMarkCreated.ElapsedMilliseconds.ToString().PadLeft(4), 
                                        swReserveSpace.ElapsedMilliseconds.ToString().PadLeft(4), 
                                        delegateStartedAt.Subtract(w.JobCreatedAt).TotalMilliseconds.ToString(CultureInfo.InvariantCulture).PadLeft(4), 
                                        entry.DisplayPath);
                                }

                                return AsyncCacheDetailResult.WriteSucceeded;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                    var swEnqueue = Stopwatch.StartNew();
                    var queueResult = CurrentWrites.Queue(w, async delegate(AsyncWrite job)
                    {
                        try
                        {
                            var unused = await EvictWriteAndLog(false, swDataCreation.Elapsed, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError("HybridCache failed to flush async write, {Exception} {DisplayPath}\n{StackTrace}", ex.ToString(),
                                entry.DisplayPath, ex.StackTrace);
                        }
                        finally
                        {
                            //TODO: Remove this duplicate line of code
                            CurrentWrites.Remove(job); //Remove from the queue, it's done or failed. 
                        }

                    });
                    swEnqueue.Stop();
                    swInsideQueueLock.Stop();
                    swGetOrCreateBytes.Stop();

                    if (queueResult == AsyncWriteCollection.AsyncQueueResult.QueueFull)
                    {
                        if (Options.WriteSynchronouslyWhenQueueFull)
                        {
                            var writerDelegateResult = await EvictWriteAndLog(true, swDataCreation.Elapsed, cancellationToken);
                            cacheResult.Detail = writerDelegateResult;
                        }
                    }
                });
            if (!queueLockComplete)
            {
                //On queue lock failure
                if (!Options.FailRequestsOnEnqueueLockTimeout)
                {
                    // We run the callback with no intent of caching
                    var (contentType, arraySegment) = await dataProviderCallback(cancellationToken);
                    
                    cacheResult.Detail = AsyncCacheDetailResult.QueueLockTimeoutAndCreated;
                    cacheResult.ContentType = contentType;
                    cacheResult.Data = new MemoryStream(arraySegment.Array ?? throw new NullReferenceException(), 
                        arraySegment.Offset, arraySegment.Count, false, true);
                }
                else
                {
                    cacheResult.Detail = AsyncCacheDetailResult.QueueLockTimeoutAndFailed;
                }
            }
            return cacheResult;
        }
    }
}