using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Imazen.PersistentCache
{
    struct CacheKeyHasher
    {
        readonly uint _shardCount;
        internal CacheKeyHasher(uint shardCount)
        {
            _shardCount = shardCount;
        }

        internal uint GetShardByKey1(byte[] key1)
        {
            using (var h = SHA256.Create())
            {
                var a = h.ComputeHash(key1);
                var shardSeed = BitConverter.ToUInt32(a, 8);

                uint shard = (uint)(shardSeed % _shardCount);
                return shard;
            }
        }
        internal string GetBlobKey(byte[] Key1Hash, byte[] Key2Hash, byte[] Key3Hash)
        {
            using (var h = SHA256.Create())
            {

                var masterHashData = new List<byte>(64);
                masterHashData.AddRange(Key1Hash);
                masterHashData.AddRange(Key2Hash);
                masterHashData.AddRange(Key3Hash);
                var masterHash = h.ComputeHash(masterHashData.ToArray());

                var blobKey = new StringBuilder(75);
                blobKey.AppendFormat("{0:x2}", masterHash[16]);
                blobKey.Append('/');
                blobKey.AppendFormat("{0:x2}", masterHash[17]);
                blobKey.Append('/');
                foreach (byte item in masterHash)
                    blobKey.AppendFormat("{0:x2}", item);
                return blobKey.ToString();
            }
        }

        internal HashedCacheKey Hash(CacheKey key)
        {

            using (var h = SHA256.Create())
            {
                var a = h.ComputeHash(key.Key1);
                var b = h.ComputeHash(key.Key2).Take(16).ToArray();
                var c = h.ComputeHash(key.Key3).Take(16).ToArray();

                var masterHashData = new List<byte>(64);
                masterHashData.AddRange(a);
                masterHashData.AddRange(b);
                masterHashData.AddRange(c);
                var masterHash = h.ComputeHash(masterHashData.ToArray());

                var readId = BitConverter.ToUInt32(masterHash, 0);

                var shardSeed = BitConverter.ToUInt32(a, 8);

                uint shard = (uint)(shardSeed % _shardCount);

                var blobKey = new StringBuilder(75);
                blobKey.Append("blobs/");
                blobKey.AppendFormat("{0:x2}", masterHash[16]);
                blobKey.Append('/');
                blobKey.AppendFormat("{0:x2}", masterHash[17]);
                blobKey.Append('/');
                foreach (byte item in masterHash)
                    blobKey.AppendFormat("{0:x2}", item);


                var result = new HashedCacheKey
                {
                    Key1Hash = a,
                    Key2Hash = b,
                    Key3Hash = c,
                    ReadId = readId,
                    Shard = shard,
                    BlobKey = blobKey.ToString()
                };
                return result;
            }
        }

        internal byte[] HashKey2(byte[] key2)
        {
            using (var h = SHA256.Create())
            {
                return h.ComputeHash(key2).Take(16).ToArray();
            }
        }

        internal byte[] HashKey1(byte[] key1)
        {
            using (var h = SHA256.Create())
            {
                return h.ComputeHash(key1);
            }
        }
    }
}
;