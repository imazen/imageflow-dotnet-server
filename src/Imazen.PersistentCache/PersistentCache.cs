using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.PersistentCache
{
    public class PersistentCache : IPersistentCache
    {
        public PersistentCache(IPersistentStore store, IClock clock, PersistentCacheOptions options)
        {
            //TODO guard against null store, null clock, zero shardCount, or zero halflife
            this.store = store;
            hasher = new CacheKeyHasher(options.ShardCount);
            shards = new Shard[options.ShardCount];
            for (uint i =0; i < options.ShardCount; i++){
                shards[i] = new Shard(store, i, clock, hasher, options);
            }
            
        }

        readonly IPersistentStore store;
        readonly CacheKeyHasher hasher;
        readonly Shard[] shards;
        public async Task EvictByKey1(byte[] key1, CancellationToken cancellationToken)
        {
            var shard = hasher.GetShardByKey1(key1);
            await shards[shard].EvictByKey1HashExcludingKey2Hash(hasher.HashKey1(key1), null, cancellationToken);

        }

        public async Task EvictByKey1ExcludingKey2(byte[] includingKey1, byte[] excludingKey2, CancellationToken cancellationToken)
        {
            var shard = hasher.GetShardByKey1(includingKey1);
            await shards[shard].EvictByKey1HashExcludingKey2Hash(hasher.HashKey1(includingKey1), hasher.HashKey2(excludingKey2), cancellationToken);
        }

        public Task<Stream> GetStream(CacheKey key, CancellationToken cancellationToken)
        {
            var hash = hasher.Hash(key);
            shards[hash.Shard].PingUsed(hash.ReadId);
            return store.ReadStream(hash.Shard, hash.BlobKey, cancellationToken);
        }

        public void PutBytesEventually(CacheKey key, byte[] data, uint cost)
        {
            var hash = hasher.Hash(key);
            shards[hash.Shard].EnqueuePutBytes(hash, data, cost);
        }

        public Task FlushWrites()
        {
            return Task.WhenAll(shards.Select((shard) => shard.FlushWrites()));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.WhenAll(shards.Select((shard) => shard.StartAsync(cancellationToken)));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.WhenAll(shards.Select((shard) => shard.StopAsync(cancellationToken)));
        }

        public List<Exception> PopExceptions()
        {
            var exceptions = new List<Exception>();
            foreach(var s in shards)
            {
                while (true)
                {
                    var e = s.PopException();
                    if (e != null)
                    {
                        exceptions.Add(e);
                    }
                    else
                    {
                        break; 
                    }

                }
            }
            return exceptions;
        }
    }
}
