using System;

namespace Imazen.Common.BlobStorage
{
    internal class BlobMetadataException : Exception
    {
        public BlobMetadataException(string message) : base(message) { }
    }
}
