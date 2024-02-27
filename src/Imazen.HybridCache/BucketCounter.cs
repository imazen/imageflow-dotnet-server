using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Imazen.HybridCache
{
    /// <summary>
    /// Useful for roughly tracking access to a high number of items. Items are rounded into buckets based on
    /// their hash collisions, and counters go from 0..65535
    /// </summary>
    internal class BucketCounter
    {
        /// <summary>
        /// We use a dictionary when at the beginning
        /// </summary>
        private ConcurrentDictionary<int, ushort>? dict;
        /// <summary>
        /// We allocate a sparse array after a certain point
        /// </summary>
        private ushort[]? table;

        /// <summary>
        /// Lock object for transitioning from dictionary to sparse array
        /// </summary>
        private readonly object upgradeToTableLock = new object();

        public void Increment(byte[] data) => Increment(GetHash(data));
        
        public void Increment(int hash)
        {
            if (hash >= RowCount)
                throw new ArgumentOutOfRangeException(nameof(hash), "Hash value is out of range for bucket count");
            
            if (table != null)
            {
                var old = table[hash];
                if (old < UInt16.MaxValue) old++;
                table[hash] = old;
            }
            else
            {
                dict?.AddOrUpdate(hash, 1, (_, oldValue) =>
                {
                    if (oldValue < UInt16.MaxValue) oldValue++;
                    return oldValue;
                });

                if (dict == null || dict.Count <= MaxDictionarySize) return;
                
                lock (upgradeToTableLock)
                {
                    if (dict == null) return;
                        
                    var oldDict = dict;
                    dict = null; // We stop listening to increments here. 
                    var newTable = new ushort[RowCount];
                    foreach (var pair in oldDict)
                    {
                        newTable[pair.Key] = pair.Value;
                    }
                    // We start listening to increments again here
                    table = newTable;
                    dict = null;
                }
            }
        }


        public ushort Get(int hash)
        {
            if (hash >= RowCount)
                throw new ArgumentOutOfRangeException(nameof(hash), "Hash value is out of range for bucket count");
            
            if (table != null)
            {
                return table[hash];
            }
            if (dict != null && dict.TryGetValue(hash, out var v))
            {
                return v;
            }
            return 0;
        }

        public ushort Get(byte[] data) => Get(GetHash(data));

        private int HashSizeInBits { get;  }
        
        internal long RowCount { get; }
        
        internal int MaxDictionarySize { get; }
        
        private uint HashMask { get;  }

        internal static uint GetHashMask(int bits)
        {
            uint v = 0;
            for (var i = 0; i < bits && i < 31; i++)
            {
                v = (v << 1) | 1;
            }
            return v;
        }

        public int GetHash(byte[] bytes)
        {
            // Since this class is about approximation
            // We should probably use something even faster
            // This is not cryptographic or security-relevant
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(bytes);
                var index = BitConverter.ToUInt32(hash, 16);
                return (int) (index & HashMask);
            }
        }


        public BucketCounter CreateWithMaxMemoryBytes(long maxMemoryBytes)
        {
            var maxRows = maxMemoryBytes / 2;
            var maxHashBits = (int) Math.Floor(Math.Log(maxRows, 2));
            if (maxHashBits < 2) throw new ArgumentOutOfRangeException(nameof(maxMemoryBytes), "Must be 8 or greater");
            return new BucketCounter(maxHashBits);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="maxHashBits"></param>
        /// <param name="useStarterDictionary">Leave false unless you've benchmarked it and are sure</param>
        /// <param name="maxStarterDictionarySize">When to switch to a table / sparse array</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        internal BucketCounter(int maxHashBits, bool useStarterDictionary = false, int maxStarterDictionarySize = 512)
        {
            if (maxHashBits < 2) throw new ArgumentOutOfRangeException(nameof(maxHashBits), "Must be 2 or greater");
            HashSizeInBits = Math.Min(31, maxHashBits);
            RowCount = (long)Math.Pow(2, HashSizeInBits);
            HashMask = GetHashMask(HashSizeInBits);
            
            // We're guessing that an allocation larger than 4KiB is worse than creating a ConcurrentDictionary
            // Although a ConcurrentDictionary spawns like 64 objects by default
            // And only provides a concurrency advantage if access is evenly distributed. 
            // It might be reasonable to raise the limit to 
            if (useStarterDictionary)
            {
               dict = new ConcurrentDictionary<int, ushort>();
               MaxDictionarySize = maxStarterDictionarySize;
            }
            else
            {
                table = new ushort[RowCount];
            }
        }
    }
}