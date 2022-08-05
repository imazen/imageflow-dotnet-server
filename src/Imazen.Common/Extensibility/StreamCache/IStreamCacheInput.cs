using System;

namespace Imazen.Common.Extensibility.StreamCache
{
    public interface IStreamCacheInput
    {
        string ContentType { get; }
        ArraySegment<byte> Bytes { get; }
    }
}