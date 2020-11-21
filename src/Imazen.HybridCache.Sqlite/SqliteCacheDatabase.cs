using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Concurrency;
using Imazen.Common.ExtensionMethods;
using Microsoft.Extensions.Logging;

namespace Imazen.HybridCache.Sqlite
{
    public class SqliteCacheDatabase: ICacheDatabase
    {
        private SqliteCacheDatabaseOptions Options { get; }
        private readonly string databasePath;
        private readonly bool databaseInMemory;
        private SQLiteConnection connection;
        private bool isOpen;
        private ILogger<HybridCache> Logger { get; }

        private DisposableAsyncLock Lock = new DisposableAsyncLock();
        public SqliteCacheDatabase(SqliteCacheDatabaseOptions options, ILogger<HybridCache> logger)
        {
            Logger = logger;
            Options = options;
            databaseInMemory = options.DatabaseDir == ":memory:";
            if (!databaseInMemory)
            {
                databasePath = Path.Combine(options.DatabaseDir, "imageflow_cache_index.sqlite");
            }
            isOpen = false;
        }
        
        private void AssertOpen()
        {
            if (!isOpen) throw new InvalidOperationException(
                "You cannot perform cache operations before StartAsync() is called or after StopAsync() is called.");
        }


        public async Task<IEnumerable<ICacheDatabaseRecord>> GetOldestRecords(DateTime maxLastDeletionAttemptTime,
            DateTime maxCreatedDate, int count)
        {
            using (var unused = await Lock.LockAsync())
            {
                var cancellationToken = CancellationToken.None;
                var lookupTime = Stopwatch.StartNew();
                AssertOpen();
                using (var read = connection.CreateCommand())
                {
                    read.CommandText =
                        @"SELECT relative_path, disksize, created, last_delete_attempt, content_type, access_count_key FROM files 
                    WHERE created < @max_created AND last_delete_attempt < @max_last_delete ORDER BY created LIMIT @count;";
                    read.Parameters.AddWithValue("@max_created", maxCreatedDate.ToUnixTimeUtc());
                    read.Parameters.AddWithValue("@max_last_delete", maxLastDeletionAttemptTime.ToUnixTimeUtc());
                    read.Parameters.AddWithValue("@count", count);

                    read.Prepare();
                    AssertOpen();
                    var reader = await read.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken);
                    AssertOpen();
                    var rows = new List<ICacheDatabaseRecord>(count);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var row = new CacheDatabaseRecord()
                        {
                            RelativePath = reader.GetString(0),
                            CreatedAt = reader.GetInt64(1).UnixTimeUtcIntoDateTime(),
                            LastDeletionAttempt = reader.GetInt64(2).UnixTimeUtcIntoDateTime(),
                            ContentType = reader.GetString(3),
                            AccessCountKey = reader.GetInt32(4),
                        };
                        rows.Add(row);
                    }

                    lookupTime.Stop();
                    Logger?.LogInformation(
                        $"HybridCache.Sqlite GetOldestRecords time {lookupTime.ElapsedMilliseconds}ms");
                    return rows;
                }
            }
        }

        public async Task<long> GetTotalBytes()
        {
            using (var unused = await Lock.LockAsync())
            {
                return await GetTotalBytesInternalUnsynchronized(CancellationToken.None);
            }
        }

        private async Task<long> GetTotalBytesInternalUnsynchronized(CancellationToken cancellationToken)
        {
            using (var sumDiskSize = connection.CreateCommand())
            {
                AssertOpen();
                sumDiskSize.CommandText = "SELECT SUM(disksize) FROM files";
                var sizeReader = await sumDiskSize.ExecuteReaderAsync(CommandBehavior.SingleRow,cancellationToken);
                AssertOpen();
                if (await sizeReader.ReadAsync(cancellationToken))
                {
                    // NULL means there were no rows in the db
                    return sizeReader.GetValue(0) != DBNull.Value ? sizeReader.GetInt64(0) : 0;
                }

                throw new InvalidOperationException("Failed to get SUM(disksize) from files");
            }
        }

        private async Task<bool> DeleteRecordUnsynchronized(string relativePath, CancellationToken cancellationToken)
        {
            AssertOpen();
            using (var delete = connection.CreateCommand())
            {
                delete.CommandText =
                    @"DELETE FROM files WHERE relative_path = @relative_path;
                      SELECT changes();";
                delete.Parameters.AddWithValue("@relative_path", relativePath);
                delete.Prepare();
                AssertOpen();
                var reader = await delete.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
                AssertOpen();
                if (await reader.ReadAsync(cancellationToken))
                {
                    return reader.GetInt32(0) > 0;
                }

                return false;
            }
        }
        public async Task<bool> DeleteRecord(string relativePath)
        {
            using (var unused = await Lock.LockAsync())
            {
                var cancellationToken = CancellationToken.None;
                return await DeleteRecordUnsynchronized(relativePath, cancellationToken);
            }
        }

        public async Task<string> GetContentType(string relativePath)
        {
            using (var unused = await Lock.LockAsync())
            {
                var cancellationToken = CancellationToken.None;
                var lookupTime = Stopwatch.StartNew();
                AssertOpen();
                using (var read = connection.CreateCommand())
                {
                    read.CommandText =
                        @"SELECT content_type FROM files WHERE relative_path = @relative_path";
                    read.Parameters.AddWithValue("@relative_path", relativePath);
                    read.Prepare();
                    AssertOpen();
                    var reader = await read.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
                    AssertOpen();
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        return reader.GetString(0);
                    }

                    lookupTime.Stop();
                    return null;
                }
            }
        }

        public int EstimateRecordDiskSpace(int stringKeyLength)
        {
            return 6000 + stringKeyLength;
        }

        private async Task<bool> CreateRecordUnsynchronized(CacheDatabaseRecord record, CancellationToken cancellationToken)
        {
            AssertOpen();
            using (var create = connection.CreateCommand())
            {
                create.CommandText =
                    @"INSERT INTO files(relative_path, disksize, created, last_delete_attempt, content_type, access_count_key)
                        VALUES (@relative_path, @disksize, @created, @last_delete_attempt, @content_type, @access_count_key);
                      SELECT changes();";
                create.Parameters.AddWithValue("@relative_path", record.RelativePath);
                create.Parameters.AddWithValue("@disksize", record.DiskSize);
                create.Parameters.AddWithValue("@created", record.CreatedAt.ToUnixTimeUtc());
                create.Parameters.AddWithValue("@last_delete_attempt", record.LastDeletionAttempt.ToUnixTimeUtc());
                create.Parameters.AddWithValue("@content_type", record.ContentType);
                create.Parameters.AddWithValue("@access_count_key", record.AccessCountKey);
                create.Prepare();
                AssertOpen();
                var reader = await create.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
                AssertOpen();
                if (await reader.ReadAsync(cancellationToken))
                {
                    return reader.GetInt32(0) > 0;
                }

                return false;
            }
        }
        public async Task<bool> CreateRecordIfSpace(string relativePath, string contentType, long recordDiskSpace, DateTime createdDate,
            int accessCountKey, long diskSpaceLimit)
        {
            using (var unused = await Lock.LockAsync())
            {
                var cancellationToken = CancellationToken.None;

                var spaceUsed = await GetTotalBytesInternalUnsynchronized(cancellationToken);
                if (spaceUsed + recordDiskSpace > diskSpaceLimit)
                {
                    return false; // We're out of space
                }

                return await CreateRecordUnsynchronized(new CacheDatabaseRecord()
                {
                    RelativePath = relativePath,
                    ContentType = contentType,
                    AccessCountKey = accessCountKey,
                    CreatedAt = createdDate,
                    DiskSize = recordDiskSpace,
                    LastDeletionAttempt = ((long) 0).UnixTimeUtcIntoDateTime()
                }, cancellationToken);
            }
        }
        public async Task ReplaceRelativePathAndUpdateLastDeletion(ICacheDatabaseRecord record, string movedRelativePath,
            DateTime lastDeletionAttempt)
        {
            var cancellationToken = CancellationToken.None;
            using (var unused = await Lock.LockAsync())
            {
                var newRecord = new CacheDatabaseRecord()
                {
                    RelativePath = movedRelativePath,
                    AccessCountKey = record.AccessCountKey,
                    ContentType = record.ContentType,
                    CreatedAt = record.CreatedAt,
                    DiskSize = record.DiskSize,
                    LastDeletionAttempt = lastDeletionAttempt
                };
                // Even if it wasn't deleted, we still create the new record
                try
                {
                    await DeleteRecordUnsynchronized(record.RelativePath, cancellationToken);
                }
                finally
                {
                    await CreateRecordUnsynchronized(newRecord, cancellationToken);
                }
            }
        }

        public async Task UpdateCreatedDate(string relativePath, DateTime createdDate)
        {
            using (var unused = await Lock.LockAsync())
            {
                var cancellationToken = CancellationToken.None;
                AssertOpen();
                using (var update = connection.CreateCommand())
                {
                    update.CommandText =
                        @"UPDATE files SET created=@created WHERE relative_path=@relative_path;";
                    update.Parameters.AddWithValue("@relative_path", relativePath);
                    update.Parameters.AddWithValue("@created", createdDate.ToUnixTimeUtc());
                    update.Prepare();
                    AssertOpen();
                    await update.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

    
        public async Task UpdateLastDeletionAttempt(string relativePath, DateTime when)
        {
            using (var unused = await Lock.LockAsync())
            {
                var cancellationToken = CancellationToken.None;
                AssertOpen();
                using (var update = connection.CreateCommand())
                {
                    update.CommandText =
                        @"UPDATE files SET last_delete_attempt=@last_delete_attempt WHERE relative_path=@relative_path;";
                    update.Parameters.AddWithValue("@relative_path", relativePath);
                    update.Parameters.AddWithValue("@last_delete_attempt", when.ToUnixTimeUtc());
                    update.Prepare();
                    AssertOpen();
                    await update.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }



        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (var unused = await Lock.LockAsync())
            {
                if (!databaseInMemory)
                {
                    if (!Directory.Exists(Options.DatabaseDir))
                        Directory.CreateDirectory(Options.DatabaseDir);
                    if (!File.Exists(databasePath))
                        SQLiteConnection.CreateFile(databasePath);
                    connection = new SQLiteConnection($"Data Source={databasePath};Version=3;");
                }
                else
                {
                    connection = new SQLiteConnection($"Data Source=:memory:;Version=3;New=True;");
                }

                await connection.OpenAsync(cancellationToken);
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode=WAL;";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                using (var createTable = connection.CreateCommand())
                {
                    createTable.CommandText =
                        "CREATE TABLE IF NOT EXISTS files (relative_path TEXT PRIMARY KEY,  disksize INTEGER, created INTEGER, last_delete_attempt INTEGER, content_type TEXT, access_count_key INTEGER);";
                    await createTable.ExecuteNonQueryAsync(cancellationToken);
                }

                isOpen = true;
                Logger?.LogInformation("Imazen.HybridCache.Sqlite started successfully.");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            using (var unused = await Lock.LockAsync())
            {
                isOpen = false;
                connection.Close();
                Logger?.LogInformation("Imazen.HybridCache.Sqlite stopped successfully.");
                return;
            }
        }
    }
}