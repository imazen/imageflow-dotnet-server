using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Imageflow.Server.Extensibility;
using Microsoft.Extensions.Logging;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Imageflow.Server.SqliteCache
{
    public class SqliteCacheService : ISqliteCache
    {
        private readonly SqliteCacheOptions options;
        private readonly ILogger<SqliteCacheService> logger;
        private readonly string databasePath;
        private readonly bool databaseInMemory;
        private SQLiteConnection connection;
        private bool isOpen = false;
        private readonly CancellationTokenSource killer;
        private long CacheSize = 0;
        private long BytesWrittenSinceCheckpoint = 0;

        public SqliteCacheService(SqliteCacheOptions options, ILogger<SqliteCacheService> logger)
        {
            this.options = options;
            this.logger = logger;
            databaseInMemory = options.DatabaseDir == ":memory:";
            if (!databaseInMemory)
            {
                this.databasePath = Path.Combine(options.DatabaseDir, "imageflow_cache.sqlite");
            }
            killer = new CancellationTokenSource();
        }

        private void AssertOpen()
        {
            if (!isOpen) throw new InvalidOperationException(
                "You cannot perform cache operations before StartAsync() is called or after StopAsync() is called.");
        }

        public async Task<SqliteCacheEntry> GetOrCreate(string key, Func<Task<SqliteCacheEntry>> create)
        {
            var lookupTime = Stopwatch.StartNew();
            // key TEXT PRIMARY KEY, lastused INTEGER, disksize INTEGER, contentype TEXT, data BLOB
            AssertOpen();
            await using var read = connection.CreateCommand();
            read.CommandText = "SELECT * FROM blobs WHERE key=@cachekey LIMIT 1";
            read.Parameters.AddWithValue("@cachekey", key);
            await read.PrepareAsync();
            AssertOpen();
            var reader = await read.ExecuteReaderAsync(CommandBehavior.SingleRow, killer.Token);
            AssertOpen();
            if (await reader.ReadAsync())
            {
                var contentType = reader.GetString(3);
                var blob = (byte[])reader.GetValue(4);
                lookupTime.Stop();
                logger?.LogInformation($"SqliteCache (hit) lookup time {lookupTime.ElapsedMilliseconds}ms");
                return new SqliteCacheEntry() {ContentType = contentType, Data = blob};
            }
            else
            {
                lookupTime.Stop();
                var createTime = Stopwatch.StartNew();
                var entry = await create();
                createTime.Stop();
                var putTime = Stopwatch.StartNew();
                var diskSize = entry.Data.Length + 300;
                await using var write = connection.CreateCommand();
                write.CommandText =
                    "INSERT OR IGNORE INTO blobs(key, lastused, disksize, contentype, data) VALUES (@key, @lastused, @disksize, @contenttype, @data)";
                write.Parameters.AddWithValue("@key", key);
                write.Parameters.AddWithValue("@lastused", DateTimeOffset.Now.Ticks);
                write.Parameters.AddWithValue("@disksize", diskSize);
                write.Parameters.AddWithValue("@contenttype", entry.ContentType);
                write.Parameters.AddWithValue("@data", entry.Data);
                await write.PrepareAsync();
                AssertOpen();
                await write.ExecuteNonQueryAsync();
                Interlocked.Add(ref CacheSize, diskSize);
                Interlocked.Add(ref BytesWrittenSinceCheckpoint, diskSize);
                putTime.Stop();

            
                if (BytesWrittenSinceCheckpoint > 8096 * 1000)
                {
                    // We *want* to fire and forget
#pragma warning disable CS4014
                    Task.Run(CheckpointDatabase);
#pragma warning restore CS4014
                    BytesWrittenSinceCheckpoint = 0;
                }
                
                logger?.LogInformation($"SqliteCache (miss) lookup time {lookupTime.ElapsedMilliseconds}ms, imaging time {createTime.ElapsedMilliseconds}ms, put time {putTime.ElapsedMilliseconds}ms");
                return entry;
            }
        }

        private async Task CheckpointDatabase()
        {
            var checkpointTime = Stopwatch.StartNew();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await cmd.ExecuteNonQueryAsync(killer.Token);
            BytesWrittenSinceCheckpoint = 0;
            checkpointTime.Stop();
            logger?.LogInformation($"SqliteCache checkpoint flushed in {checkpointTime.ElapsedMilliseconds}ms");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!databaseInMemory)
            {
                if (!Directory.Exists(options.DatabaseDir))
                    Directory.CreateDirectory(options.DatabaseDir);
                if (!File.Exists(databasePath))
                    SQLiteConnection.CreateFile(databasePath);
                connection = new SQLiteConnection($"Data Source={databasePath};Version=3;");
            }
            else
            {
                connection = new SQLiteConnection($"Data Source=:memory:;Version=3;New=True;");
            }

            await connection.OpenAsync(cancellationToken);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            await using var createTable = connection.CreateCommand();
            createTable.CommandText =
                "CREATE TABLE IF NOT EXISTS blobs (key TEXT PRIMARY KEY, lastused INTEGER, disksize INTEGER, contentype TEXT, data BLOB);";
            await createTable.ExecuteNonQueryAsync(cancellationToken);

            await using var sumDiskSize = connection.CreateCommand();
            sumDiskSize.CommandText = "SELECT SUM(disksize) FROM blobs";
            var sizeReader = await sumDiskSize.ExecuteReaderAsync(cancellationToken);
            if (await sizeReader.ReadAsync(cancellationToken) && sizeReader.GetValue(0) != DBNull.Value)
            {
                CacheSize = sizeReader.GetInt64(0);
            }
            isOpen = true;
            logger?.LogInformation("SqliteCache started successfully.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            isOpen = false;
            killer.Cancel();
            await connection.CloseAsync();
            logger?.LogInformation("SqliteCache stopped successfully.");
        }
    }
}