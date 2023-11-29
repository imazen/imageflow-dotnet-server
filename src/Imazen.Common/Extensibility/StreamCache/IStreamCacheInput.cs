namespace Imazen.Common.Extensibility.StreamCache
{
    
    [Obsolete("Implement IBlobReusable instead")]
    public interface IStreamCacheInput
    {
        string ContentType { get; }
        ArraySegment<byte> Bytes { get; }
    }
}