using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Instrumentation.Support;
using Imazen.Common.Storage;
using Imazen.Common.Storage.Caching;
using Microsoft.IO;

namespace Imageflow.Server.Caching
{
    /// <summary>
    /// Uses a ConcurrentBitArray of size 2^24 to track whether a key is likely to exist in the cache.
    /// Syncs via blob serialization. Bits representing buckets can only be set to 1, never 0. Works for any string key using SHA256 subsequence.
    /// </summary>
    internal class ExistenceProbableMap
    {
        private readonly ConcurrentBitArray buckets;

        private const int hashBits = 24;

        private const int bucketCount = 1 << hashBits;
        public ExistenceProbableMap(){
            buckets = new ConcurrentBitArray(bucketCount);
        }

        private static string BlobName => $"existence-probable-map-{hashBits}-bit.bin";


               //ReadAllBytesAsync ArrayPool

        internal static async Task ReadAllBytesIntoBitArrayAsync(Stream stream, ConcurrentBitArray target, MemoryPool<byte> pool, CancellationToken cancellationToken = default)
        {   
            if (stream == null) return;
            try
            {

                if (stream.CanSeek)
                {
                    if (stream.Length != target.ByteCount)
                    {
                        throw new Exception($"ExistenceProbableMap.ReadAllBytesIntoBitArrayAsync: Expected {target.ByteCount} bytes in stream, got {stream.Length}");
                    }
                }
                
                var bufferOwner = pool.Rent(target.ByteCount);
                var bytesRead = await stream.ReadAsync(bufferOwner.Memory, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    bufferOwner.Dispose();
                    throw new IOException("ExistenceProbableMap.ReadAllBytesIntoBitArrayAsync: Stream was empty.");
                }
                if (bytesRead != target.ByteCount)
                {
                    bufferOwner.Dispose();
                    throw new IOException($"ExistenceProbableMap.ReadAllBytesIntoBitArrayAsync: Expected {target.ByteCount} bytes, got {bytesRead}");
                }
                // ensure no more bytes can be read
                if (stream.ReadByte() != -1)
                {
                    bufferOwner.Dispose();
                    throw new IOException("ExistenceProbableMap.ReadAllBytesIntoBitArrayAsync: Stream was longer than {target.ByteCount} bytes.");
                }
                // copy to target
                target.LoadFromSpan(bufferOwner.Memory[..bytesRead].Span);
            }
            finally
            {
                stream.Dispose();
            }
        }

        internal static async Task<ConcurrentBitArray> FetchSync(IBlobCache cache, ConcurrentBitArray target = null, CancellationToken cancellationToken = default){
            var fetchResult = await cache.TryFetchBlob(BlobGroup.Essential, BlobName,  cancellationToken);
            using (fetchResult){
                if (fetchResult.DataExists){    
                    target ??= new ConcurrentBitArray(bucketCount);                
                    await ReadAllBytesIntoBitArrayAsync(fetchResult.Data.OpenRead(),target,  MemoryPool<byte>.Shared, cancellationToken);
                    return target;
                }
            }
            return null;
        }
        

        public async Task Sync(IBlobCache cache, CancellationToken cancellationToken = default){
            
            // Fetch
            var existingData = await FetchSync(cache, cancellationToken: cancellationToken);
            if (existingData != null){
                //Merge
                buckets.MergeTrueBitsFrom(existingData);
            }
            //Put
            var bytes = buckets.ToBytes();
            await cache.Put(BlobGroup.Essential, BlobName, new BytesBlobData(bytes), CacheBlobPutOptions.Default, cancellationToken);
        }
       
        
        internal static int HashFor(byte[] data)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(data);
            var getBits = System.BitConverter.ToUInt32(hash, 0);
            return (int)(getBits >> (32 - hashBits));
        }
        internal static int HashFor(string s) => HashFor(System.Text.Encoding.UTF8.GetBytes(s));

        public bool MayExist(string key){
            var hash = HashFor(key);
            return buckets[hash];
        }
        public void MarkExists(string key){
            var hash = HashFor(key);
            buckets[hash] = true;
        }

        public void MarkExists(byte[] data){
            var hash = HashFor(data);
            buckets[hash] = true;
        }
        public bool MayExist(byte[] data){
            var hash = HashFor(data);
            return buckets[hash];
        }


    }
}