using System;

namespace Imageflow.Server.Structs
{
    public struct ImageData
    {
        public ArraySegment<byte> resultBytes;
        public string contentType;
    }
}
