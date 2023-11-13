using System;
using System.IO;

namespace Imazen.Common.Storage.Caching
{
    internal class BytesBlobData : IBlobData
    {
        public byte[] Bytes { get; private set; }
        public BytesBlobData(byte[] bytes, DateTime? lastModifiedDateUtc = null)
        {
            Bytes = bytes;
            LastModifiedDateUtc = lastModifiedDateUtc;
        }
        public bool? Exists => true; 

        public DateTime? LastModifiedDateUtc { get; private set; }

        public void Dispose()
        {
            
        }

        public Stream OpenRead()
        {
            return new MemoryStream(Bytes);
        }
    }
}