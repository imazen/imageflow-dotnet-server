#pragma warning disable CS0618 // Type or member is obsolete
namespace Imazen.Common.Extensibility.StreamCache
{
    [Obsolete("Implement IBlobCacheResult instead")]
    public interface IStreamCacheResult
    {
        /// <summary>
        /// An open stream to the data. Must be disposed by caller. Null if something went so wrong that even an uncached result isn't available
        /// </summary>
        Stream Data { get; }

        /// <summary>
        /// null, or the content type if one was requested. 
        /// </summary>
        string? ContentType { get; }

        /// <summary>
        /// The result of the cache check.
        /// </summary>
        string Status { get; }
    }
    
    internal class StreamCacheResult : IStreamCacheResult
    {
        public StreamCacheResult(Stream data, string? contentType, string status)
        {
            Data = data;
            ContentType = contentType;
            Status = status;
        }

        public Stream Data { get; }
        public string? ContentType { get; }
        public string Status { get; }
    }
}