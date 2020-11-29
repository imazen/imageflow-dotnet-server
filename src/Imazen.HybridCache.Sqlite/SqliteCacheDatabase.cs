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
using NeoSmart.AsyncLock;

namespace Imazen.HybridCache.Sqlite
{
    public class SqliteCacheDatabase: ICacheDatabase
    {
        private SqliteCacheDatabaseOptions Options { get; }
        private readonly string databasePath;
        private readonly bool databaseInMemory;
        private SQLiteConnection sharedConnection;
        private bool isOpen;
        private ILogger Logger { get; }
        
        private CacheSizeCache SizeCache { get; }
        
        private readonly IDisposableAsyncLock dbLock;
        public SqliteCacheDatabase(SqliteCacheDatabaseOptions options, ILogger logger)
        {
            Logger = logger;
            Options = options;
            if (options.SynchronizeDatabaseCalls)
            {
                dbLock = new AsyncLockWrapper(new AsyncLock());
            }
            else
            {
                dbLock = new NonLock();
            }
            
            databaseInMemory = options.DatabaseDir == ":memory:";
            if (!databaseInMemory)
            {
                databasePath = Path.Combine(options.DatabaseDir, "imageflow_cache_index.sqlite");
            }
            isOpen = false;

            SizeCache = new CacheSizeCache(GetTotalBytesUncached);
        }
        
        private void AssertOpen()
        {
            if (!isOpen) throw new InvalidOperationException(
                "You cannot perform cache operations before StartAsync() is called or after StopAsync() is called.");
        }


        public async Task<IEnumerable<ICacheDatabaseRecord>> GetOldestRecords(DateTime maxLastDeletionAttemptTime,
            DateTime maxCreatedDate, int count)
        {
            using (var unused = await dbLock.LockAsync())
            {
                AssertOpen();
                using (var provider = GetConnectionProvider())
                {
                    using (var read = provider.Connection.CreateCommand())
                    {
                        read.CommandText =
                            @"SELECT relative_path, disksize, created, last_delete_attempt, content_type, access_count_key FROM files 
                    WHERE created < @max_created AND last_delete_attempt < @max_last_delete ORDER BY created LIMIT @count;";
                        read.Parameters.AddWithValue("@max_created", maxCreatedDate.ToUnixTimeUtc());
                        read.Parameters.AddWithValue("@max_last_delete", maxLastDeletionAttemptTime.ToUnixTimeUtc());
                        read.Parameters.AddWithValue("@count", count);

                        read.Prepare();
                        AssertOpen();
                        // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                        var reader = read.ExecuteReader(CommandBehavior.Default);
                        AssertOpen();
                        var rows = new List<ICacheDatabaseRecord>(count);
                        while (reader.Read())
                        {
                            var row = new CacheDatabaseRecord()
                            {
                                RelativePath = reader.GetString(0),
                                DiskSize = reader.GetInt64(1),
                                CreatedAt = reader.GetInt64(2).UnixTimeUtcIntoDateTime(),
                                LastDeletionAttempt = reader.GetInt64(3).UnixTimeUtcIntoDateTime(),
                                ContentType = reader.GetString(4),
                                AccessCountKey = reader.GetInt32(5),
                            };
                            rows.Add(row);
                        }

                        return rows;
                    }
                }
            }
        }

        public Task<long> GetTotalBytes()
        {
            return Task.FromResult(SizeCache.GetTotalBytes());
        }
        private long GetTotalBytesUncached()
        {
            using (var unused = dbLock.Lock())
            {
                return GetTotalBytesInternalUnsynchronized();
            }
        }

        private long GetTotalBytesInternalUnsynchronized()
        {
            AssertOpen();
            using (var provider = GetConnectionProvider())
            {
                using (var sumDiskSize = provider.Connection.CreateCommand())
                {

                    AssertOpen();
                    sumDiskSize.CommandText = "SELECT SUM(disksize) FROM files";
                    // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                    var sizeReader = sumDiskSize.ExecuteReader(CommandBehavior.SingleRow);
                    AssertOpen();
                    if (sizeReader.Read())
                    {
                        // NULL means there were no rows in the db
                        return sizeReader.GetValue(0) != DBNull.Value ? sizeReader.GetInt64(0) : 0;
                    }

                    throw new InvalidOperationException("Failed to get SUM(disksize) from files");
                }
            }
        }

        private bool DeleteRecordUnsynchronized(ICacheDatabaseRecord record)
        {
            AssertOpen();
            using (var provider = GetConnectionProvider())
            {
                using (var delete = provider.Connection.CreateCommand())
                {
                    delete.CommandText =
                        @"DELETE FROM files WHERE relative_path = @relative_path;";
                    delete.Parameters.AddWithValue("@relative_path", record.RelativePath);
                    delete.Prepare();
                    AssertOpen();
                    SizeCache.BeforeDeleted(record.DiskSize);
                    // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                    var result = delete.ExecuteNonQuery();

                    return result > 0;
                }
            }
        }
        public async Task<bool> DeleteRecord(ICacheDatabaseRecord record)
        {
            using (var unused = await dbLock.LockAsync())
            {
                return DeleteRecordUnsynchronized(record);
            }
        }

        public async Task<string> GetContentType(string relativePath)
        {
            using (var unused = await dbLock.LockAsync())
            {
                var lookupTime = Stopwatch.StartNew();
                AssertOpen();
                using (var provider = GetConnectionProvider())
                {
                    using (var read = provider.Connection.CreateCommand())
                    {
                        read.CommandText =
                            @"SELECT content_type FROM files WHERE relative_path = @relative_path";
                        read.Parameters.AddWithValue("@relative_path", relativePath);
                        read.Prepare();
                        AssertOpen();
                        // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                        var reader = read.ExecuteReader(CommandBehavior.SingleRow);
                        AssertOpen();
                        if (reader.Read())
                        {
                            return reader.GetString(0);
                        }

                        lookupTime.Stop();
                        return null;
                    }
                }
            }
        }

        public int EstimateRecordDiskSpace(int stringKeyLength)
        {
            return 6000 + stringKeyLength;
        }

        private enum CreateRecordResult
        {
            Created,
            DuplicateRecord
        }
        private CreateRecordResult CreateRecordUnsynchronized(CacheDatabaseRecord record)
        {
            try
            {
                AssertOpen();
                using (var provider = GetConnectionProvider())
                {
                    using (var create = provider.Connection.CreateCommand())
                    {
                        create.CommandText =
                            @"INSERT INTO files(relative_path, disksize, created, last_delete_attempt, content_type, access_count_key)
                        VALUES (@relative_path, @disksize, @created, @last_delete_attempt, @content_type, @access_count_key);
                      SELECT changes();";
                        create.Parameters.AddWithValue("@relative_path", record.RelativePath);
                        create.Parameters.AddWithValue("@disksize", record.DiskSize);
                        create.Parameters.AddWithValue("@created", record.CreatedAt.ToUnixTimeUtc());
                        create.Parameters.AddWithValue("@last_delete_attempt",
                            record.LastDeletionAttempt.ToUnixTimeUtc());
                        create.Parameters.AddWithValue("@content_type", record.ContentType);
                        create.Parameters.AddWithValue("@access_count_key", record.AccessCountKey);
                        create.Prepare();
                        AssertOpen();
                        SizeCache.BeforeAdded(record.DiskSize);
                        AssertOpen();
                        var nonQueryResult = create.ExecuteNonQuery();
                        AssertOpen();
                        return CreateRecordResult.Created;
                    }
                }
            }
            catch (SQLiteException sException)
            {
                if (sException.ErrorCode == 19)
                    return CreateRecordResult.DuplicateRecord;
                throw;
            }
        }
        public async Task<bool> CreateRecordIfSpace(string relativePath, string contentType, long recordDiskSpace, DateTime createdDate,
            int accessCountKey, long diskSpaceLimit)
        {
            var spaceUsed = SizeCache.GetTotalBytes();
            using (var unused = await dbLock.LockAsync())
            {
                
                if (spaceUsed + recordDiskSpace > diskSpaceLimit)
                {
                    return false; // We're out of space
                }

                var createRecordResult = CreateRecordUnsynchronized(new CacheDatabaseRecord()
                {
                    RelativePath = relativePath,
                    ContentType = contentType,
                    AccessCountKey = accessCountKey,
                    CreatedAt = createdDate,
                    DiskSize = recordDiskSpace,
                    LastDeletionAttempt = ((long) 0).UnixTimeUtcIntoDateTime()
                });
                return createRecordResult == CreateRecordResult.Created ||
                        createRecordResult == CreateRecordResult.DuplicateRecord;
            }
        }
        public async Task ReplaceRelativePathAndUpdateLastDeletion(ICacheDatabaseRecord record, string movedRelativePath,
            DateTime lastDeletionAttempt)
        {
            using (var unused = await dbLock.LockAsync())
            {
                var newRecord = new CacheDatabaseRecord
                {
                    RelativePath = movedRelativePath,
                    AccessCountKey = record.AccessCountKey,
                    ContentType = record.ContentType,
                    CreatedAt = record.CreatedAt,
                    DiskSize = record.DiskSize,
                    LastDeletionAttempt = lastDeletionAttempt
                };
                // We create the new record first, since the new file already exists
                CreateRecordUnsynchronized(newRecord);
                // Then we delete the old
                DeleteRecordUnsynchronized(record);
            }
        }

        public async Task UpdateCreatedDate(string relativePath, DateTime createdDate)
        {
            using (var unused = await dbLock.LockAsync())
            {
                AssertOpen();
                using (var provider = GetConnectionProvider())
                {
                    using (var update = provider.Connection.CreateCommand())
                    {
                        update.CommandText =
                            @"UPDATE files SET created=@created WHERE relative_path=@relative_path;";
                        update.Parameters.AddWithValue("@relative_path", relativePath);
                        update.Parameters.AddWithValue("@created", createdDate.ToUnixTimeUtc());
                        update.Prepare();
                        AssertOpen();
                        update.ExecuteNonQuery();
                    }
                }
            }
        }

    
        public async Task UpdateLastDeletionAttempt(string relativePath, DateTime when)
        {
            using (var unused = await dbLock.LockAsync())
            {
                AssertOpen();
                using (var provider = GetConnectionProvider())
                {
                    using (var update = provider.Connection.CreateCommand())
                    {
                        update.CommandText =
                            @"UPDATE files SET last_delete_attempt=@last_delete_attempt WHERE relative_path=@relative_path;";
                        update.Parameters.AddWithValue("@relative_path", relativePath);
                        update.Parameters.AddWithValue("@last_delete_attempt", when.ToUnixTimeUtc());
                        update.Prepare();
                        AssertOpen();
                        update.ExecuteNonQuery();
                    }
                }
            }
        }


        private IOpenConnectionProvider GetConnectionProvider()
        { 
            return Options.ShareDatabaseConnection ? (IOpenConnectionProvider)new SharedConnectionProvider(sharedConnection) : 
                new InstancedConnectionProvider(CreateAndOpenConnection());
            
        }

        private SQLiteConnection CreateAndOpenConnection()
        {
            SQLiteConnection connection;
            if (!databaseInMemory)
            {
                if (!Directory.Exists(Options.DatabaseDir))
                    Directory.CreateDirectory(Options.DatabaseDir);
                var builder = new SQLiteConnectionStringBuilder()
                {
                    DataSource = databasePath,
                };
                connection = new SQLiteConnection(builder.ToString());
            }
            else
            {
                connection = new SQLiteConnection($"Data Source=:memory:");
            }

            connection.Open();
            return connection;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (var unused = await dbLock.LockAsync())
            {
                sharedConnection = CreateAndOpenConnection();

                using (var createTable = sharedConnection.CreateCommand())
                {
                    createTable.CommandText =
                        "CREATE TABLE IF NOT EXISTS files (relative_path TEXT PRIMARY KEY,  disksize INTEGER, created INTEGER, last_delete_attempt INTEGER, content_type TEXT, access_count_key INTEGER);";
                    createTable.ExecuteNonQuery();
                }

                isOpen = true;
                Logger?.LogInformation("Imazen.HybridCache.Sqlite started successfully.");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            using (var unused = await dbLock.LockAsync())
            {
                isOpen = false;
                sharedConnection?.Close();
                Logger?.LogInformation("Imazen.HybridCache.Sqlite stopped successfully.");
            }
        }
        
       
    }

    internal interface IDisposableAsyncLock
    {
        Task<IDisposable> LockAsync();

        IDisposable Lock();
    }
    internal class NonLock : IDisposableAsyncLock
    {
        private class NonLockDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
        public Task<IDisposable> LockAsync()
        {
            return Task.FromResult((IDisposable) new NonLockDisposable());
        }

        public IDisposable Lock()
        {
            return new NonLockDisposable();
        }
    }
    internal class AsyncLockWrapper : IDisposableAsyncLock
    {
        private AsyncLock asyncLock;
        public AsyncLockWrapper(AsyncLock asyncLock)
        {
            this.asyncLock = asyncLock;
        }

        public async Task<IDisposable> LockAsync()
        {
            return await asyncLock.LockAsync();
        }

        public IDisposable Lock()
        {
            return asyncLock.Lock();
        }
    }

    internal interface IOpenConnectionProvider: IDisposable
    { 
        SQLiteConnection Connection { get; }
    }

    internal class InstancedConnectionProvider : IOpenConnectionProvider
    {
        public InstancedConnectionProvider(SQLiteConnection connection)
        {
            Connection = connection;
        }
        public void Dispose()
        {
            Connection.Close();
            Connection.Dispose();
        }

        public SQLiteConnection Connection { get; }
    }
    
    internal class SharedConnectionProvider : IOpenConnectionProvider
    {
        public SharedConnectionProvider(SQLiteConnection connection)
        {
            Connection = connection;
        }
        public void Dispose()
        {
        }

        public SQLiteConnection Connection { get; }
    }
}