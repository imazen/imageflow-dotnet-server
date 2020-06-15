using System;

namespace Imazen.Common.Storage
{
    public class BlobMissingException : Exception
    {
        public BlobMissingException()
        {
        }

        public BlobMissingException(string message)
            : base(message)
        {
        }

        public BlobMissingException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}