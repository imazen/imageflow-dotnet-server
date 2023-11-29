namespace Imazen.Common.Storage
{
    [Obsolete("Use Imazen.Abstractions.Blobs.BlobWrapper instead")]
    public interface IBlobData : IDisposable
    {
        bool? Exists { get; }
        DateTime? LastModifiedDateUtc { get; }

        Stream OpenRead();
    }
}
