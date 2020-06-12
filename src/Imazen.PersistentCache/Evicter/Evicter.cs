using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.PersistentCache.Evicter
{
    /// <summary>
    /// This class will have StartAsync(ct) and StopAsync(ct) methods to allow graceful shutdown
    /// It will prevent multiple evictions from happening simulateneously. 
    /// It will provide a way to await the next eviction cycle so put commands can be queued
    /// 
    /// </summary>
    internal class Evicter : IDisposable
    {
        private readonly uint shardId;
        private readonly IPersistentStore store;
        private readonly UsageTracker usage;
        private readonly IClock clock;
        private readonly PersistentCacheOptions options;
        private readonly CancellationTokenSource shutdownTokenSource;
        private readonly SizeTracker sizeTracker;
        private readonly CacheKeyHasher hasher; 
        private readonly AsyncLock evictionLock = new AsyncLock();
        private readonly AsyncLock flushLogLock = new AsyncLock();
        public Evicter(uint shardId, IPersistentStore store, UsageTracker usage, CacheKeyHasher hasher, IClock clock, PersistentCacheOptions options, CancellationTokenSource shutdownTokenSource)
        {
            this.shardId = shardId;
            this.store = store;
            this.usage = usage;
            this.clock = clock;
            this.options = options;
            this.shutdownTokenSource = shutdownTokenSource;
            this.hasher = hasher;
            sizeTracker = new SizeTracker(shardId, store, clock, options);
        }

        private Task logFlushRuntimeTask;


        // Summary:
        // Triggered when the application host is ready to start the service.
#pragma warning disable IDE0060 // Remove unused parameter
        internal Task StartAsync(CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            logFlushRuntimeTask = Task.Run(() => LogFlushRuntime(shutdownTokenSource.Token), shutdownTokenSource.Token);
            return Task.CompletedTask;
        }

        internal Task RecordBytesUsed(int length)
        {
            return sizeTracker.OffsetBy(length);
        }

        async Task FlushLogUnmerged()
        {
            try
            {
                if (writeLog.Count == 0) return;

                using (var f = await flushLogLock.LockAsync())
                {
                    var entries = new List<WriteEntry>(writeLog.Count);
                    while (writeLog.TryDequeue(out WriteEntry result))
                    {
                        entries.Add(result);
                    }
                    await WriteMultipleLogs(entries, CancellationToken.None);
                }
            }
            catch (Exception)
            {

            }
        }
        async Task FlushLogMerged()
        {
            try
            {
                if (writeLog.Count == 0) return;
                using (var f = await flushLogLock.LockAsync())
                {
                    var entries = new List<WriteEntry>(writeLog.Count);
                    while (writeLog.TryDequeue(out WriteEntry result))
                    {
                        entries.Add(result);
                    }
                    await WriteLogsMerged(entries, CancellationToken.None);
                }

            }
            catch (Exception)
            {

            }
        }

        string GetNewLogName() => $"writelog/{clock.GetUtcNow():yyyy-MM-dd_HH-mm-ss-ffff}_{Guid.NewGuid()}";
        string WriteLogsDir() => "writelog/";
        async Task WriteMultipleLogs(List<WriteEntry> entries, CancellationToken cancellationToken)
        {
            if (entries.Count == 0) return;
            var entriesPerLog = (int)options.MaxWriteLogSize / WriteEntry.RowBytes();

            var enumerable = entries.AsEnumerable();
            while (enumerable.Any())
            {
                var batch = enumerable.Take(entriesPerLog);
                enumerable = enumerable.Skip(entriesPerLog);

                var bytes = new List<byte>(batch.Count());
                foreach (var entry in batch)
                {
                    entry.SerializeTo(bytes);
                }
                
                await store.WriteBytes(shardId, GetNewLogName(), bytes.ToArray(), cancellationToken);

            }

        }

        internal Task FlushWriteLog()
        {
            return FlushLogUnmerged();
        }

        async Task WriteLogsMerged(List<WriteEntry> entries, CancellationToken cancellationToken) { 

            // Write logs unmerged unless we have too few entries. 
            var entryThreshold = (int)(.9 * (double)options.MaxWriteLogSize / (double)WriteEntry.RowBytes());
            if (entries.Count > entryThreshold)
            {
                await FlushLogUnmerged();
                return;
            }


            // List logs and sort for the smallest
            var logList = (await store.List(shardId, WriteLogsDir(), cancellationToken)).ToList();
            logList.Sort((a, b) => a.SizeInBytes.CompareTo(b.SizeInBytes));
            var logs = new List<IBlobInfo>();
            ulong totalBytes = (ulong)(entries.Count * WriteEntry.RowBytes());
            // Add logs into `logs` until we have enough bytes
            foreach(var log in logList)
            {
                if (totalBytes + log.SizeInBytes > options.MaxWriteLogSize)
                {
                    break;
                }
                else
                {
                    logs.Add(log);
                    totalBytes += log.SizeInBytes;
                }
            }

            // Use a memory stream to buffer new and old entries in
            var replacementBytes = new MemoryStream((int)totalBytes);
            // Buffer new entries
            var entryBuffer = new List<byte>(WriteEntry.RowBytes());
            foreach (var entry in entries)
            {
                entryBuffer.Clear();
                entry.SerializeTo(entryBuffer);
                replacementBytes.Write(entryBuffer.ToArray(), 0, entryBuffer.Count);
            }
            // Append bytes from old logs
            foreach(var mergeLog in logs)
            {
                using (var stream = await store.ReadStream(shardId, mergeLog.KeyName, cancellationToken))
                {
                    if (stream != null)
                    {
                        await stream.CopyToAsync(replacementBytes, 81920, cancellationToken);
                    }
                }
            }

            // Write new log
            var fileBytes = replacementBytes.ToArray(); 
            await store.WriteBytes(shardId, GetNewLogName(), fileBytes.ToArray(), cancellationToken);

            // Delete old logs
            foreach (var mergeLog in logs)
            {
                await store.Delete(shardId, mergeLog.KeyName, cancellationToken);
            }
        }

        async Task LogFlushRuntime(CancellationToken cancellationToken)
        {
            
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(options.WriteLogFlushIntervalMs, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await FlushLogMerged();
            }
        }


        readonly ConcurrentQueue<WriteEntry> writeLog = new ConcurrentQueue<WriteEntry>();
        internal Task WriteLogEventually(HashedCacheKey hash, byte[] data, uint cost)
        {
            writeLog.Enqueue(new WriteEntry
            {
                Key1 = hash.Key1Hash,
                Key2 = hash.Key2Hash,
                Key3 = hash.Key3Hash,
                ReadId = hash.ReadId,
                ByteCount = (ulong)data.LongLength,
                CreationCost = cost
            });

            return Task.CompletedTask;
        }


        internal async Task EvictSpaceFor(int length, CancellationToken cancellationToken)
        {
            using (var lockInstance = await evictionLock.LockAsync())
            {
                var currentSize = await sizeTracker.GetCachedSize();
                if (currentSize + (ulong)length < options.MaxCachedBytes)
                {
                    return; // There's enough space already. 
                }

                var bytesToDelete = (currentSize + (ulong)length) - (ulong)(options.MaxCachedBytes - (options.FreeSpacePercentGoal / 100 * options.MaxCachedBytes));
                ulong bytesDeleted = 0;

                while (bytesDeleted < bytesToDelete)
                {
                    // Read in a full log's worth of entries. 
                    var list = (await store.List(shardId,WriteLogsDir(), cancellationToken)).ToList();
                    if (list.Count < 1) return; 
                    var rng = new Random();
                    var logs = new List<IBlobInfo>();
                    var writeEntries = new List<WriteEntry>((int)(options.MaxWriteLogSize / (ulong)WriteEntry.RowBytes()));

                    ulong logsSizeSum = 0;
                    while (true)
                    {
                        var next = list[rng.Next(0, list.Count - 1)];
                        if (logsSizeSum + next.SizeInBytes < options.MaxWriteLogSize)
                        {
                            using (var stream = await store.ReadStream(shardId, next.KeyName, cancellationToken))
                            {
                                if (stream != null)
                                {
                                    writeEntries.AddRange(await WriteEntry.ReadFrom(stream, cancellationToken));
                                    logs.Add(next);
                                    logsSizeSum += next.SizeInBytes;
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Add usage info
                    for (var i = 0; i < writeEntries.Count; i++)
                    {
                        var entry = writeEntries[i];
                        entry.FrequencyExtra = usage.GetFrequency(entry.ReadId);
                        writeEntries[i] = entry;
                    }

                    // Sort the best entries to delete 
                    writeEntries.Sort((a, b) =>
                    {
                        var result = a.FrequencyExtra.CompareTo(b.FrequencyExtra);
                        if (result == 0)
                        {
                            result = ((double)a.ByteCount / Math.Max(1,(double)a.CreationCost))
                                .CompareTo((double)b.ByteCount / Math.Max(1,(double)b.CreationCost));

                        }
                        return result;
                    });

                    // Delete the first 10%
                    var entriesToDeleteCount = writeEntries.Count / 10;
                    ulong bytesDeletedThisLoop = 0;
                    foreach (var entry in writeEntries.Take(entriesToDeleteCount))
                    {
                        var blobKey = hasher.GetBlobKey(entry.Key1, entry.Key2, entry.Key3);
                        await store.Delete(shardId, blobKey, cancellationToken);
                        bytesDeletedThisLoop += entry.ByteCount;
                    }

                    // Write the rest of the entries back to disk
                    await WriteMultipleLogs(writeEntries.Skip(entriesToDeleteCount).ToList(), cancellationToken);
                    
                    // Delete the old log files
                    foreach (var logToDelete in logs)
                    {
                        await store.Delete(shardId, logToDelete.KeyName, cancellationToken);
                    }

                    bytesDeleted += bytesDeletedThisLoop;
                    await sizeTracker.OffsetBy(-(long)bytesDeletedThisLoop);

                } // Repeat until we have enough space
            }
        }

        public void Dispose()
        {
            shutdownTokenSource.Cancel();
        }

        internal async Task EvictByKey1HashExcludingKey2Hash(byte[] includingKey1Hash, byte[] excludingKey2Hash, CancellationToken cancellationToken)
        {
            // Read in a full log's worth of entries. 
            var enumerable = await store.List(shardId, WriteLogsDir(), cancellationToken);

            foreach (var log in enumerable)
            {
                using (var stream = await store.ReadStream(shardId, log.KeyName, cancellationToken))
                {
                    if (stream != null)
                    {
                        var entries = await WriteEntry.ReadFrom(stream, cancellationToken);

                        // Select and delete the matching entries
                        var matches = entries.Where((entry) => entry.Key1.SequenceEqual(includingKey1Hash) && (excludingKey2Hash == null || !entry.Key2.SequenceEqual(excludingKey2Hash))).ToList();
                        if (matches.Count > 0)
                        {
                            foreach(var entry in matches)
                            {
                                var blobKey = hasher.GetBlobKey(entry.Key1, entry.Key2, entry.Key3);
                                await store.Delete(shardId, blobKey, cancellationToken);
                            }

                        }
                        // Select the remaining entries
                        var remaining = entries.Where((entry) => !entry.Key1.SequenceEqual(includingKey1Hash) || (excludingKey2Hash != null && entry.Key2.SequenceEqual(excludingKey2Hash))).ToList();
                        if (remaining.Count > 0)
                        {
                            // Write the rest of the entries back to disk
                            var remainingLogBytes = new List<byte>(WriteEntry.RowBytes() * (remaining.Count));
                            foreach (var entry in remaining)
                            {
                                entry.SerializeTo(remainingLogBytes);
                            }
                            
                            await store.WriteBytes(shardId, GetNewLogName(), remainingLogBytes.ToArray(), cancellationToken);
                        }
                        // Delete old log file
                        await store.Delete(shardId, log.KeyName, cancellationToken);
                    }
                }

            }
        
        }

        // Summary:
        // Triggered when the application host is performing a graceful shutdown.
        internal async Task StopAsync(CancellationToken cancellationToken)
        {
            if (logFlushRuntimeTask == null)
            {
                return;
            }
            
            try
            {
                // Signal cancellation to the executing method
                shutdownTokenSource.Cancel();
            }
            finally
            {
                try
                {
                    // Wait until the task completes or the stop token triggers
                    await Task.WhenAny(logFlushRuntimeTask, Task.Delay(Timeout.Infinite,
                                                                  cancellationToken));
                }
                finally
                {
                    //Always ensure we've finished flushing the log
                    await FlushLogUnmerged();
                }
            }
        }
    }
}
