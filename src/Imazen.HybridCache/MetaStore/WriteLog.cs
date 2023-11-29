using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Imazen.Abstractions.Blobs;
using Microsoft.Extensions.Logging;

namespace Imazen.HybridCache.MetaStore
{
    internal class WriteLog
    {
        // Increment this if the log format changes
        const int LogFormatVersion = 2;
        private readonly string databaseDir;
        private readonly MetaStoreOptions options;
        private readonly ILogger logger;
        private long startedAt;
        private bool startupComplete;
        private FileStream? writeLogStream;
        private BinaryWriter? binaryLogWriter;
        private readonly object writeLock = new object();
        private readonly int shardId;
        private long previousLogsBytes;
        private long logBytes;
        private long diskBytes;
        private readonly long directoryEntriesBytes;

        public WriteLog(int shardId, string databaseDir, MetaStoreOptions options, long directoryEntriesBytes,
            ILogger logger)
        {
            this.shardId = shardId;
            this.databaseDir = databaseDir;
            this.options = options;
            this.logger = logger;
            this.directoryEntriesBytes = directoryEntriesBytes;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            lock (writeLock)
            {
                binaryLogWriter?.Flush();
                binaryLogWriter?.Dispose();
                writeLogStream?.Flush(true);
                writeLogStream?.Dispose();
                writeLogStream = null;
                binaryLogWriter = null;
            }

            return Task.CompletedTask;
        }

        private void CreateWriteLog(long startedAtTick)
        {
            var writeLogPath = Path.Combine(databaseDir,
                $"{startedAtTick.ToString().PadLeft(20, '0')}.v{LogFormatVersion}.metastore");
            try
            {
                writeLogStream = new FileStream(writeLogPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    4096, FileOptions.SequentialScan);
                binaryLogWriter = new BinaryWriter(writeLogStream, Encoding.UTF8, true);
            }
            catch (IOException ioException)
            {
                logger?.LogError(ioException, "Failed to open log file {Path} for writing", writeLogPath);
                throw;
            }
        }
        
        // create a record containing a version int and a FileStream instance
        private class FileStreamWithVersion(int version, FileStream stream)
        {
            public int Version { get; } = version;
            public FileStream Stream { get; } = stream;
        }
        
        // Returns a dictionary of the database 
        public async Task<ConcurrentDictionary<string, CacheDatabaseRecord>> Startup()
        {
            if (startupComplete) throw new InvalidOperationException("Startup() can only be called once");
            startedAt = Stopwatch.GetTimestamp();

            if (!Directory.Exists(databaseDir))
                Directory.CreateDirectory(databaseDir);

            // Sort the log files numerically, since they are tick counts. 
            var rawFiles = Directory.GetFiles(databaseDir, "*", SearchOption.TopDirectoryOnly);
            var orderedLogs = rawFiles.Select(path =>
                {
                    var filename = Path.GetFileNameWithoutExtension(path);
                    // Reject non metastore files
                    if (!path.EndsWith(".metastore")) return null;
                    return long.TryParse(filename, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result)
                        ? new Tuple<string, long>(path, result)
                        : null;
                })
                .Where(t => t != null).Cast<Tuple<string, long>>()
                .OrderBy(t => t.Item2)
                .ToArray();


            // Read in all the log files
            var openedFiles = new List<FileStreamWithVersion>(orderedLogs.Length);
            long openedLogFileBytes = 0;
            List<LogEntry>[] logSets;
            try
            {
                // Open all the input log files at once. We want to fail fast if there is a problem. 
                var lastOpenPath = "";
                try
                {
                    foreach (var t in orderedLogs)
                    {
                        lastOpenPath = t.Item1;
                        // parse the version from .v3.metastore in the path. Assume v1 if no .v[0-9]+.metastore suffix
                        var version = 1;
                        var versionMatch = Regex.Match(lastOpenPath, @"\.v([0-9]+)\.metastore$");
                        if (versionMatch.Success)
                        {
                            version = int.Parse(versionMatch.Groups[1].Value);
                        }
                        var fs = new FileStream(lastOpenPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                            FileOptions.SequentialScan);
                        openedFiles.Add(new FileStreamWithVersion(version, fs));
                        openedLogFileBytes += CleanupManager.EstimateFileSizeOnDiskFor(fs.Length);
                    }
                }
                catch (IOException ioException)
                {
                    logger?.LogError(ioException,
                        "Failed to open log file {Path} for reading in shard {ShardId}. Perhaps another process is still writing to the log?",
                        lastOpenPath, shardId);
                    throw new ImageflowMultipleHybridCacheInstancesNotSupportedIoException(ioException,
                        "Multiple instances of HybridCache are attempting to write to the same database directory. This is not supported.",
                        lastOpenPath, shardId);
                }

                // Open the write log file, even if we won't use it immediately.
                CreateWriteLog(startedAt);

                //Read all logs in
                logSets = await Task.WhenAll(openedFiles.Select(ReadLogEntries));
            }
            finally
            {
                for (var ix = 0; ix < openedFiles.Count; ix++)
                {
                    try
                    {
                        var stream = openedFiles[ix].Stream;
                        stream.Close();
                        stream.Dispose();
                    }
                    catch (Exception e)
                    {
                        logger?.LogError(e, "Failed to close log file {Path}", orderedLogs[ix].Item1);
                    }
                }
            }

            // Create dictionary from logs
            var totalEntryEst = logSets.Sum(set => set.Count);
            var dict = new ConcurrentDictionary<string, CacheDatabaseRecord>(Environment.ProcessorCount,
                totalEntryEst, StringComparer.Ordinal);
            foreach (var entry in logSets.SelectMany(l => l))
            {
                if (entry.EntryType == LogEntryType.Delete)
                {
                    if (!dict.TryRemove(entry.RelativePath, out var unused))
                    {
                        logger?.LogWarning("Log entry deletion referred to nonexistent record: {Path}",
                            entry.RelativePath);
                    }
                }

                if (entry.EntryType == LogEntryType.Update)
                {
                    if (!dict.ContainsKey(entry.RelativePath))
                    {
                        logger?.LogWarning("Log entry update refers to non-existent record {Path}", entry.RelativePath);
                    }

                    dict[entry.RelativePath] = entry.ToRecord();
                }

                if (entry.EntryType == LogEntryType.Create)
                {
                    if (!dict.TryAdd(entry.RelativePath, entry.ToRecord()))
                    {
                        logger?.LogWarning("Log entry creation failed as record already exists {Path}",
                            entry.RelativePath);
                    }
                }
            }


            // Do consolidation if needed
            if (orderedLogs.Length >= options.MaxLogFilesPerShard)
            {
                //Write new file and delete old ones
                foreach (var record in dict.Values)
                {
                    await WriteLogEntry(new LogEntry(LogEntryType.Create, record), false);
                }
                if (writeLogStream == null)
                {
                    throw new InvalidOperationException("writeLogStream is null");
                }
                    
                await writeLogStream.FlushAsync();

                // Delete old logs
                foreach (var t in orderedLogs)
                {
                    try
                    {
                        File.Delete(t.Item1);
                    }
                    catch (IOException ioException)
                    {
                        logger?.LogError(ioException, "Failed to delete log file {Path} after consolidation", t.Item1);
                    }
                }
            }
            else
            {
                previousLogsBytes += openedLogFileBytes;
                diskBytes += dict.Values.Sum(r => r.EstDiskSize);
            }

            startupComplete = true;
            return dict;
        }

        private Task<List<LogEntry>> ReadLogEntries(FileStreamWithVersion file)
        {
            using (var binaryReader = new BinaryReader(file.Stream, Encoding.UTF8, true))
            {
                var rows = new List<LogEntry>((int)(file.Stream.Length / 100));
                while (true)
                {
                    var entry = new LogEntry();
                    try
                    {
                        entry.EntryType = (LogEntryType)binaryReader.ReadByte();
                    }
                    catch (EndOfStreamException)
                    {
                        return Task.FromResult(rows);
                    }

                    try
                    {
                        entry.RelativePath = binaryReader.ReadString();
                        entry.ContentType = binaryReader.ReadString();
                        // We convert empty strings to null
                        if (entry.ContentType == "") entry.ContentType = null;
                        if (file.Version >= 2)
                        {
                            // Version 2 and above have a category and tags
                            entry.CreatedAt = new DateTimeOffset(binaryReader.ReadInt64(), TimeSpan.Zero);
                            entry.LastDeletionAttempt = new DateTimeOffset(binaryReader.ReadInt64(), TimeSpan.Zero);
                            entry.AccessCountKey = binaryReader.ReadInt32();
                            entry.EstBlobDiskSize = binaryReader.ReadInt64();
                            // Version 2 and above have a disk size
                            entry.Category = (CacheEntryFlags)binaryReader.ReadByte();
                            // and tags
                            var tagCount = binaryReader.ReadByte();
                            if (tagCount > 0)
                            {
                                var tags = new List<SearchableBlobTag>(tagCount);
                                for (var i = 0; i < tagCount; i++)
                                {
                                    var key = binaryReader.ReadString();
                                    var value = binaryReader.ReadString();
                                    
                                    tags.Add(SearchableBlobTag.CreateUnvalidated(key, value));
                                }

                                entry.Tags = tags;
                            }
                        }
                        else
                        {
                            entry.CreatedAt = DateTime.FromBinary(binaryReader.ReadInt64());
                            entry.LastDeletionAttempt = DateTime.FromBinary(binaryReader.ReadInt64());
                            entry.AccessCountKey = binaryReader.ReadInt32();
                            entry.EstBlobDiskSize = binaryReader.ReadInt64();
                            entry.Category = CacheEntryFlags.Unknown;
                        }
                        rows.Add(entry);
                    }
                    catch (EndOfStreamException e)
                    {
                        logger?.LogError(e, "Unexpected end of stream when reading log file");
                        return Task.FromResult(rows);
                    }
                }
            }
        }

        private const byte EndOfRecord = 0xB0;
        private Task WriteLogEntry(LogEntry entry, bool flush)
        {
            lock (writeLock)
            {
                if (startedAt == 0)
                    throw new InvalidOperationException("WriteLog cannot be used before calling Startup()");
                if (writeLogStream == null || binaryLogWriter == null)
                    throw new InvalidOperationException("WriteLog cannot be after StopAsync is called");
                    
                var startPos = writeLogStream.Position;
                binaryLogWriter.Write((byte)entry.EntryType);
                binaryLogWriter.Write(entry.RelativePath);
                binaryLogWriter.Write(entry.ContentType ?? "");
                binaryLogWriter.Write(entry.CreatedAt.UtcTicks);
                binaryLogWriter.Write(entry.LastDeletionAttempt.UtcTicks);
                
                binaryLogWriter.Write(entry.AccessCountKey);
                binaryLogWriter.Write(entry.EstBlobDiskSize);
                binaryLogWriter.Write((byte)entry.Category);
                var tagCount = entry.Tags?.Count ?? 0;
                if (tagCount > 255)
                {
                    throw new InvalidOperationException("Cannot write more than 255 tags per record");
                }
                binaryLogWriter.Write((byte)tagCount);
                foreach (var tag in entry.Tags ?? Enumerable.Empty<SearchableBlobTag>())
                {
                    binaryLogWriter.Write(tag.Key);
                    binaryLogWriter.Write(tag.Value);
                }
                
                if (flush)
                {
                    binaryLogWriter.Flush();
                }

                // Increase the log bytes by the number of bytes we wrote
                logBytes += Math.Max(0, writeLogStream.Position - startPos);
                // On create events, increase the disk bytes
                if (entry.EntryType == LogEntryType.Create)
                    diskBytes += entry.EstBlobDiskSize;
                if (entry.EntryType == LogEntryType.Delete)
                    diskBytes -= entry.EstBlobDiskSize;
                return Task.CompletedTask;
            }
        }

        public Task LogDeleted(ICacheDatabaseRecord deletedRecord)
        {
            return WriteLogEntry(new LogEntry(LogEntryType.Delete, deletedRecord), false);
        }

        public Task LogCreated(CacheDatabaseRecord newRecord)
        {
            // We flush created entries through to disk right away so we don't orphan disk blobs
            return WriteLogEntry(new LogEntry(LogEntryType.Create, newRecord), true);
        }

        public Task LogUpdated(CacheDatabaseRecord updatedRecord)
        {
            return WriteLogEntry(new LogEntry(LogEntryType.Update, updatedRecord), false);
        }

        public long GetDiskSize()
        {
            return diskBytes + CleanupManager.EstimateFileSizeOnDiskFor(logBytes) + previousLogsBytes +
                   directoryEntriesBytes;
        }
    }
}