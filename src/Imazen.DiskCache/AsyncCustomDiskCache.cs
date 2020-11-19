/* Copyright (c) 2014 Imazen See license.txt for your rights. */
using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Concurrency;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.DiskCache.Index;
using Microsoft.Extensions.Logging;

namespace Imazen.DiskCache {

    
    /// <summary>
    /// Handles access to a disk-based file cache. Handles locking and versioning. 
    /// Supports subfolders for scalability.
    /// </summary>
    internal class AsyncCustomDiskCache:ICleanableCache {

        
        public string PhysicalCachePath { get; }
        private readonly int subfolders;
        private readonly ILogger logger;

        public AsyncCustomDiskCache(ILogger logger, string physicalCachePath, int subfolders, long asyncMaxQueuedBytes = 1024*1024*100)
        {
            Locks = new AsyncLockProvider();
            QueueLocks = new AsyncLockProvider();
            Index = new CacheIndex();
            CurrentWrites = new AsyncWriteCollection();
            this.logger = logger;
            PhysicalCachePath = physicalCachePath;
            this.subfolders = subfolders;
            CurrentWrites.MaxQueueBytes = asyncMaxQueuedBytes;
        }
        /// <summary>
        /// Fired immediately before GetCachedFile return the result value. 
        /// </summary>
        public event CacheResultHandler CacheResultReturned; 


        /// <summary>
        /// Provides string-based locking for file write access.
        /// </summary>
        public AsyncLockProvider Locks {get; }

        /// <summary>
        /// Provides string-based locking for image resizing (not writing, just processing). Prevents duplication of efforts in asynchronous mode, where 'Locks' is not being used.
        /// </summary>
        private AsyncLockProvider QueueLocks { get;  }

        /// <summary>
        /// Contains all the queued and in-progress writes to the cache. 
        /// </summary>
        private AsyncWriteCollection CurrentWrites {get; }

        /// <summary>
        /// Provides an in-memory index of the cache.
        /// </summary>
        public CacheIndex Index { get; }
        
        /// <summary>
        /// May return either a physical file name or a MemoryStream with the data. 
        /// Faster than GetCachedFile, as writes are (usually) asynchronous. If the write queue is full, the write is forced to be synchronous again.
        /// Identical to GetCachedFile() when asynchronous=false
        /// </summary>
        /// <param name="keyBasis"></param>
        /// <param name="extension"></param>
        /// <param name="writeCallback"></param>
        /// <param name="timeoutMs"></param>
        /// <param name="asynchronous"></param>
        /// <returns></returns>
        public async Task<CacheResult> GetCachedFile(string keyBasis, string extension, AsyncWriteResult writeCallback, int timeoutMs, bool asynchronous)
        {
            Stopwatch sw = null;
            if (logger != null) { sw = Stopwatch.StartNew(); }

            //Relative to the cache directory. Not relative to the app or domain root
            var keyBasisBytes = new UTF8Encoding().GetBytes(keyBasis);
            var relativePath = new HashBasedPathBuilder().BuildPath(keyBasisBytes, subfolders, "/") + '.' + extension;

            //Physical path
            var physicalPath = PhysicalCachePath.TrimEnd('\\', '/') + Path.DirectorySeparatorChar +
                               relativePath.Replace('/', Path.DirectorySeparatorChar);


            var result = new CacheResult(CacheQueryResult.Hit, physicalPath, relativePath);

            var asyncFailed = false;
            

            //2013-apr-25: What happens if the file is still being written to disk - it's present but not complete? To handle that, we use mayBeLocked.

            var mayBeLocked = Locks.MayBeLocked(relativePath.ToUpperInvariant());

             //On the first check, verify the file exists using System.IO directly (the last 'true' parameter).
            if (!asynchronous) {
                //On the first check, verify the file exists using System.IO directly (the last 'true' parameter)
                //May throw an IOException if the file cannot be opened, and is locked by an external processes for longer than timeoutMs. 
                //This method may take longer than timeoutMs under absolute worst conditions. 
                if (!await TryWriteFile(result, physicalPath, relativePath, writeCallback, timeoutMs, !mayBeLocked)) {
                    //On failure
                    result.Result = CacheQueryResult.Failed;
                }
            }
            else if (!Index.ExistsCertain(relativePath, physicalPath) || mayBeLocked)
            {
                
                //Looks like a miss. Let's enter a lock for the creation of the file. This is a different locking system than for writing to the file - far less contention, as it doesn't include the 
                //This prevents two identical requests from duplicating efforts. Different requests don't lock.

                //Lock execution using relativePath as the sync basis. Ignore casing differences. This prevents duplicate entries in the write queue and wasted CPU/RAM usage.
                if (!await QueueLocks.TryExecuteAsync(relativePath.ToUpperInvariant(), timeoutMs, CancellationToken.None, 
                    async () => {

                        //Now, if the item we seek is in the queue, we have a memcached hit. If not, we should check the index. It's possible the item has been written to disk already.
                        //If both are a miss, we should see if there is enough room in the write queue. If not, switch to in-thread writing. 

                        AsyncWrite t = CurrentWrites.Get(relativePath);

                        if (t != null) result.Data = t.GetReadonlyStream();

                        

                        //On the second check, use cached data for speed. The cached data should be updated if another thread updated a file (but not if another process did).
                        //When t == null, and we're inside QueueLocks, all work on the file must be finished, so we have no need to consult mayBeLocked.
                        if (t == null && !Index.Exists(relativePath, physicalPath))
                        {

                            result.Result = CacheQueryResult.Miss;
                            //Still a miss, we even rechecked the file system. Write to memory.
                            MemoryStream ms = new MemoryStream(4096);  //4K initial capacity is minimal, but this array will get copied around a lot, better to underestimate.
                            //Read, resize, process, and encode the image. Lots of exceptions thrown here.
                            await writeCallback(ms);
                            ms.Position = 0;

                            AsyncWrite w = new AsyncWrite(ms, physicalPath, relativePath);
                            if (CurrentWrites.Queue(w, async delegate(AsyncWrite job) {
                                try
                                {
                                    var swIo = Stopwatch.StartNew();
                                    //We want this to run synchronously, since it's in a background thread already.
                                    if (!(await TryWriteFile(null, job.PhysicalPath, job.Key, 
                                        delegate(Stream s) {
                                            var fromStream = job.GetReadonlyStream();
                                            return fromStream.CopyToAsync(s);
                                        }, timeoutMs, true)))
                                    {
                                        swIo.Stop();
                                        //We failed to lock the file.
                                        logger?.LogWarning("Failed to flush async write, timeout exceeded after {0}ms - {1}",  swIo.ElapsedMilliseconds, result.RelativePath);

                                    } else {
                                        swIo.Stop();
                                        logger?.LogTrace("{0}ms: Async write started {1}ms after enqueue for {2}", swIo.ElapsedMilliseconds.ToString().PadLeft(4), DateTime.UtcNow.Subtract(w.JobCreatedAt).Subtract(swIo.Elapsed).TotalMilliseconds, result.RelativePath);
                                    }

                                } catch (Exception ex)
                                {
                                    logger?.LogError("Failed to flush async write, {0} {1}\n{2}",ex.ToString(), result.RelativePath,ex.StackTrace);
                                } finally {
                                    CurrentWrites.Remove(job); //Remove from the queue, it's done or failed. 
                                }

                            })) {
                                //We queued it! Send back a read-only memory stream
                                result.Data = w.GetReadonlyStream();
                            } else {
                                asyncFailed = false;
                                //We failed to queue it - either the ThreadPool was exhausted or we exceeded the MB limit for the write queue.
                                //Write the MemoryStream to disk using the normal method.
                                //This is nested inside a queueLock because if we failed here, the next one will also. Better to force it to wait until the file is written to disk.
                                if (!await TryWriteFile(result, physicalPath, relativePath, async delegate(Stream s) { await ms.CopyToAsync(s); }, timeoutMs, false))
                                {
                                    logger?.LogWarning("Failed to queue async write, also failed to lock for sync writing: {0}", result.RelativePath);

                                }
                            }

                        }

                    })) {
                    //On failure
                    result.Result = CacheQueryResult.Failed;
                }

            }
            if (logger != null) {
                sw?.Stop();
                logger.LogTrace("{0}ms: {1}{2} for {3}, Key: {4}", 
                    sw?.ElapsedMilliseconds.ToString(NumberFormatInfo.InvariantInfo).PadLeft(4), 
                    asynchronous ? (asyncFailed ? "AsyncHttpMode, fell back to sync write  " : "AsyncHttpMode+AsyncWrites ") : "AsyncHttpMode",
                    result.Result.ToString(), 
                    result.RelativePath,  
                    keyBasis);
            }
            //Fire event
            CacheResultReturned?.Invoke(this, result);
            return result;
        }


        /// <summary>
        /// Returns true if either (a) the file was written, or (b) the file already existed
        /// Returns false if the in-process lock failed. Throws an exception if any kind of file or processing exception occurs.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="physicalPath"></param>
        /// <param name="relativePath"></param>
        /// <param name="writeCallback"></param>
        /// <param name="timeoutMs"></param>
        /// <param name="recheckFileSystem"></param>
        /// <returns></returns>
        private async Task<bool> TryWriteFile(CacheResult result, string physicalPath, string relativePath, AsyncWriteResult writeCallback, int timeoutMs, bool recheckFileSystem)
        {
            // ReSharper disable once InvertIf
            if (recheckFileSystem)
            {
                var miss = !Index.ExistsCertain(relativePath, physicalPath);
                if (!miss && !Locks.MayBeLocked(relativePath.ToUpperInvariant())) return true;
            }
               

            //Lock execution using relativePath as the sync basis. Ignore casing differences. This locking is process-local, but we also have code to handle file locking.
            return await Locks.TryExecuteAsync(relativePath.ToUpperInvariant(), timeoutMs, CancellationToken.None, 
                async () => {

                    //On the second check, use cached data for speed. The cached data should be updated if another thread updated a file (but not if another process did).
                    if (!Index.Exists(relativePath, physicalPath))
                    {

                        var subdirectoryPath = Path.GetDirectoryName(physicalPath);
                        //Create subdirectory if needed.
                        if (subdirectoryPath != null && !Directory.Exists(subdirectoryPath)) {
                            Directory.CreateDirectory(subdirectoryPath);
                        }

                        //Open stream 
                        //Catch IOException, and if it is a file lock,
                        // then it's another process writing to the file, and we can serve the file afterwards
                        //TODO: Catch UnauthorizedAccessException and log issue about file permissions.
                        //... If we can wait for a read handle for a specified timeout.
                        IOException lockedException = null;

                        try
                        {
                            var tempFile = physicalPath + ".tmp_" + new Random().Next(int.MaxValue).ToString("x") + ".tmp";

                            var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                            var finished = false;
                            try
                            {
                                using (fs)
                                {
                                    //Run callback to write the cached data
                                    await writeCallback(fs); //Can throw any number of exceptions.
                                    await fs.FlushAsync();
                                    fs.Flush(true);
                                    if (fs.Position == 0)
                                    {
                                        throw new InvalidOperationException("Disk cache wrote zero bytes to file");
                                    }
                                    finished = true;
                                }
                            }
                            finally
                            {
                                //Don't leave half-written files around.
                                if (!finished)
                                {
                                    try { if (File.Exists(tempFile)) File.Delete(tempFile); }
                                    catch
                                    {
                                        // ignored
                                    }
                                }
                            }
                            var moved = false;
                            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                            if (finished)
                            {
                                try
                                {
                                    File.Move(tempFile, physicalPath);
                                    moved = true;
                                }
                                catch (IOException)
                                {
                                    //Will throw IO exception if already exists. Which we consider a hit, so we delete the tempFile
                                    try { if (File.Exists(tempFile)) File.Delete(tempFile); }
                                    catch
                                    {
                                        // ignored
                                    }
                                }
                            }
                            if (moved)
                            {
                                var createdUtc = DateTime.UtcNow;
                                //Set the created date, so we know the last time we updated the cache.s
                                File.SetCreationTimeUtc(physicalPath, createdUtc);
                                //Update index
                                //TODO: what should sourceModifiedUtc be when there is no modified date?
                                Index.SetCachedFileInfo(relativePath, new CachedFileInfo(createdUtc, createdUtc));
                                //This was a cache miss
                                if (result != null) result.Result = CacheQueryResult.Miss;
                            }
                        }
                        catch (IOException ex)
                        {

                            if (IsFileLocked(ex)) lockedException = ex;
                             else throw;
                        }
                        if (lockedException != null)
                        {
                            //Somehow in between verifying the file didn't exist and trying to create it, the file was created and locked by someone else.
                            //When hashModifiedDate==true, we don't care what the file contains, we just want it to exist. If the file is available for 
                            //reading within timeoutMs, simply do nothing and let the file be returned as a hit.
                            var waitForFile = new Stopwatch();
                            var opened = false;
                            while (!opened && waitForFile.ElapsedMilliseconds < timeoutMs)
                            {
                                waitForFile.Start();
                                var waitABitMore = false;
                                try
                                {
                                    using (var unused = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                        opened = true;
                                }
                                catch (IOException iex)
                                {
                                    if (IsFileLocked(iex))
                                        waitABitMore = true;

                                    else throw;
                                }
                                if (waitABitMore) { await Task.Delay((int)Math.Min(30, Math.Round(timeoutMs / 3.0))); }
                                waitForFile.Stop();
                            }
                            if (!opened) throw lockedException; //By not throwing an exception, it is considered a hit by the rest of the code.

                        }


                    }
                });
        }

        private static bool IsFileLocked(IOException exception) {
            // See https://docs.microsoft.com/en-us/dotnet/standard/io/handling-io-errors
            const int errorSharingViolation = 0x20; 
            const int errorLockViolation = 0x21; 
            var errorCode = exception.HResult & 0x0000FFFF; 
            return errorCode == errorSharingViolation || errorCode == errorLockViolation;
        }


    }
}
