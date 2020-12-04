using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Concurrency;

namespace Imazen.HybridCache
{
    internal class CacheFileWriter
    {
        internal struct FileWriteResult
        {
            internal FileWriteStatus Status { get; set; }
            internal long BytesWritten { get; set; }
        }
        internal enum FileWriteStatus
        {
            FileCreated, 
            FileAlreadyExists,
            LockTimeout,
        }

        private Action<string, string> moveFileOverwriteFunc;
        private AsyncLockProvider WriteLocks { get; }

        public CacheFileWriter(AsyncLockProvider writeLocks, Action<string,string> moveFileOverwriteFunc)
        {
            WriteLocks = writeLocks;
            this.moveFileOverwriteFunc = moveFileOverwriteFunc ?? File.Move;
        }


        /// <summary>
        /// Returns true if either (a) the file was written, or (b) the file already existed
        /// Returns false if the in-process lock failed. Throws an exception if any kind of file or processing exception occurs.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="writeCallback"></param>
        /// <param name="timeoutMs"></param>
        /// <param name="recheckFileSystemFirst"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task<FileWriteResult> TryWriteFile(CacheEntry entry,
            Func<Stream, CancellationToken,Task> writeCallback, 
            bool recheckFileSystemFirst, 
            int timeoutMs, 
            CancellationToken cancellationToken)
        {
            var result = new FileWriteResult()
            {
                Status = FileWriteStatus.FileAlreadyExists
            };
            
            // ReSharper disable once InvertIf
            if (recheckFileSystemFirst)
            {
                var miss = !File.Exists(entry.PhysicalPath);
                if (!miss && !WriteLocks.MayBeLocked(entry.StringKey)) return result;
            }
            
            var lockingSucceeded = await WriteLocks.TryExecuteAsync(entry.StringKey, timeoutMs, cancellationToken,
                async () =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException(cancellationToken);

                    //On the second check, use cached data for speed. The cached data should be updated if another thread updated a file (but not if another process did).
                    if (!File.Exists(entry.PhysicalPath))
                    {

                        var subdirectoryPath = Path.GetDirectoryName(entry.PhysicalPath);
                        //Create subdirectory if needed.
                        if (subdirectoryPath != null && !Directory.Exists(subdirectoryPath))
                        {
                            Directory.CreateDirectory(subdirectoryPath);
                        }


                        var tempFile = entry.PhysicalPath + ".tmp_" + new Random().Next(int.MaxValue).ToString("x") +
                                       ".tmp";

                        var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
                        var finished = false;
                        try
                        {
                            using (fs)
                            {
                                //Run callback to write the cached data
                                await writeCallback(fs, cancellationToken); //Can throw any number of exceptions.
                                await fs.FlushAsync(cancellationToken);
                                fs.Flush(true);
                                result.BytesWritten = fs.Position;
                            }

                            try
                            {
                                
                                moveFileOverwriteFunc(tempFile, entry.PhysicalPath);
                                result.Status = FileWriteStatus.FileCreated;
                                finished = true;
                            }
                            //Will throw IO exception if already exists. Which we consider a hit, so we delete the tempFile
                            catch (IOException)
                            {
                            }
                        }
                        finally
                        {
                            //Don't leave half-written files around.
                            if (!finished)
                            {
                                try
                                {
                                    if (File.Exists(tempFile)) File.Delete(tempFile);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }
                    }
                });

            if (!lockingSucceeded)
            {
                result.Status = FileWriteStatus.LockTimeout;
            }
            return result;
        }
    }
}