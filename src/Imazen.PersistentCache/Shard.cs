using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.PersistentCache
{
    internal class Shard: IDisposable
    {
        readonly UsageTracker usage;
        readonly IPersistentStore store;
        readonly uint shardId;
        private Task frequencyTrackingPersistenceTask;
        private readonly Evicter.Evicter evicter;
        private readonly PersistentCacheSettings settings;
        
        private readonly CancellationTokenSource shutdownTokenSource =
                                                       new CancellationTokenSource();
        private readonly ConcurrentQueue<Exception> exceptionLog = new ConcurrentQueue<Exception>();

        private readonly ConcurrentQueue<Task> putByteTasks = new ConcurrentQueue<Task>();
        public Shard(IPersistentStore store, uint shardId, IClock clock, CacheKeyHasher hasher, PersistentCacheSettings settings)
        {
            usage = new UsageTracker(clock, settings.UsageFrequencyHalfLifeMinutes);
            this.store = store;
            this.shardId = shardId;
            this.settings = settings;
            evicter = new Evicter.Evicter(shardId, store, usage, hasher, clock, settings, shutdownTokenSource);
        }

        internal Exception PopException()
        {
            if (exceptionLog.TryDequeue(out Exception result))
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        internal void PingUsed(uint readId)
        {
            usage.Ping(readId);
        }

        internal void EnqueuePutBytes(HashedCacheKey hash, byte[] data, uint cost)
        {
            // TODO: have a temporary in-memory cache while the files are being written so that repeated requests for the same file aren't a miss
            putByteTasks.Enqueue(Task.Run(async () =>
            {
                try
                {
                    await evicter.EvictSpaceFor(data.Length, shutdownTokenSource.Token);
                    await evicter.WriteLogEventually(hash, data, cost);
                    await evicter.RecordBytesUsed(data.Length);
                    // Don't cancel while writing bytes
                    await store.WriteBytes(shardId, hash.BlobKey, data, CancellationToken.None);
                }
                catch (Exception e)
                {
                    if (e is OperationCanceledException)
                        throw;
                    else
                    {
                        exceptionLog.Enqueue(e);
                    }
                }

            }, shutdownTokenSource.Token));

            /// Try to cleanup tasks when possible. A long running task may block cleanup of others
            if (putByteTasks.TryPeek(out Task t))
            {
                if (t.IsCompleted || t.IsFaulted || t.IsCanceled)
                {
                    if (putByteTasks.TryDequeue(out Task task))
                    {
                        if (!(task.IsCompleted || task.IsFaulted || task.IsCanceled))
                        {
                            putByteTasks.Enqueue(task);
                        }
                    }
                }
            }
        }

        internal async Task FlushWrites()
        {
            // Await all the writes we couldn't cancel early enough
            while (putByteTasks.TryDequeue(out Task t))
            {
                await t;
            }
            await evicter.FlushWriteLog();
        }

        async Task FrequencyTrackingPersistenceRuntime(CancellationToken cancellationToken)
        {
            try
            {
                var stream = await store.ReadStream(shardId, "reads", cancellationToken);
                if (stream != null)
                {
                    await usage.MergeLoad(stream, cancellationToken);
                }
                var lastPingCount = usage.PingCount();
                while (!cancellationToken.IsCancellationRequested)
                {
                    var pingCount = usage.PingCount();

                    await Task.Delay(settings.ReadInfoFlushIntervalMs, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    //Only flush if we've gotten more than 100 reads
                    if (pingCount > lastPingCount + 100)
                    {
                        // We don't want this write to be cancelable
                        await store.WriteBytes(shardId, "reads", usage.Serialize(), CancellationToken.None);
                    }
                    lastPingCount = pingCount;

                }
            }
            catch (Exception e)
            {
                if (e is OperationCanceledException)
                    throw;
                else
                {
                    exceptionLog.Enqueue(e);
                }
            }

        }

        internal Task EvictByKey1HashExcludingKey2Hash(byte[] includingKey1Hash, byte[] excludingKey2Hash, CancellationToken cancellationToken)
        {
            return evicter.EvictByKey1HashExcludingKey2Hash(includingKey1Hash, excludingKey2Hash, cancellationToken);
        }

        // Summary:
        // Triggered when the application host is ready to start the service.
        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            frequencyTrackingPersistenceTask = Task.Run(() => FrequencyTrackingPersistenceRuntime(shutdownTokenSource.Token), shutdownTokenSource.Token); 
         
            await evicter.StartAsync(cancellationToken);
          
        }

        // Summary:
        // Triggered when the application host is performing a graceful shutdown.
        internal async Task StopAsync(CancellationToken cancellationToken)
        {
            if (frequencyTrackingPersistenceTask == null)
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
                    await evicter.StopAsync(cancellationToken);
                }
                finally
                {
                    try
                    {
                        // Wait until the task completes or the stop token triggers
                        await Task.WhenAny(frequencyTrackingPersistenceTask, Task.Delay(Timeout.Infinite,
                                                                      cancellationToken));
                    }
                    finally
                    {
                        // Await all the writes we couldn't cancel early enough
                        while (putByteTasks.TryDequeue(out Task t))
                        {
                            await t;
                        }
                    }

                }
            }
        }

        public void Dispose()
        {
            shutdownTokenSource.Cancel();
            evicter.Dispose();
        }
    }
}
