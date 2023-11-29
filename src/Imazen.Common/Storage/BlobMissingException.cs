namespace Imazen.Common.Storage
{
    [Obsolete("This type has moved to the Imazen.Abstractions.Blobs.LegacyProviders namespace.")]
    public class BlobMissingException : Imazen.Abstractions.Blobs.LegacyProviders.BlobMissingException
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