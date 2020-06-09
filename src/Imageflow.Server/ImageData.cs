using System;

namespace Imageflow.Server
{
    internal struct ImageData
    {
        public ArraySegment<byte> ResultBytes;
        public string FileExtension;
        public string ContentType;
    }
}
