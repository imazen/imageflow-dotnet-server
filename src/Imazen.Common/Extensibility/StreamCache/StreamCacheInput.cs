using System;

namespace Imazen.Common.Extensibility.StreamCache
{
    public class StreamCacheInput : IStreamCacheInput
    {
        public StreamCacheInput(string contentType, ArraySegment<byte> bytes)
        {
            ContentType = contentType;
            Bytes = bytes;
        }

        public string ContentType { get; }
        public ArraySegment<byte> Bytes { get; }

        public IStreamCacheInput ToIStreamCacheInput()
        {
            return this;
        }
    }
}