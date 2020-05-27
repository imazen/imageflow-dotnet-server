using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.PersistentCache.Evicter
{
    struct WriteEntry
    {
        internal byte[] Key1;
        internal byte[] Key2;
        internal byte[] Key3;
        internal ulong ByteCount;
        internal uint ReadId;
        internal uint CreationCost;
        internal uint FrequencyExtra; 

        internal static int RowBytes()
        {
            return 32 + 16 + 16 + 8 + 4 + 4; // 80 bytes per record
        }
        internal void SerializeTo(List<byte> buffer)
        {
            if (Key1.Length != 32 || Key2.Length != 16 || Key3.Length != 16) throw new InvalidOperationException("Hash lengths are wrong");
            buffer.AddRange(Key1);
            buffer.AddRange(Key2);
            buffer.AddRange(Key3);
            buffer.AddRange(BitConverter.GetBytes(ByteCount));
            buffer.AddRange(BitConverter.GetBytes(ReadId));
            buffer.AddRange(BitConverter.GetBytes(CreationCost));
        }

        static internal async Task<List<WriteEntry>> ReadFrom(Stream stream, CancellationToken cancellationToken)
        {
            var list = new List<WriteEntry>((int)(stream.Length / RowBytes()));

            var buffer = new byte[RowBytes()];
            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead != RowBytes())
                {
                    break;
                }
                else
                {
                    list.Add(new WriteEntry
                    {
                        Key1 = buffer.Take(32).ToArray(),
                        Key2 = buffer.Skip(32).Take(16).ToArray(),
                        Key3 = buffer.Skip(48).Take(16).ToArray(),
                        ByteCount = BitConverter.ToUInt64(buffer, 64),
                        ReadId = BitConverter.ToUInt32(buffer, 72),
                        CreationCost = BitConverter.ToUInt32(buffer, 76)
                    });


                }


            }
            return list;
        }


    }
}
