#pragma warning disable CS0618 // Type or member is obsolete
namespace Imazen.Common.Extensibility.StreamCache
{
    [Obsolete("Implement IBlobReusable instead")]
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
        public IStreamCacheResult ToIStreamCacheInResult()
        {
            return new StreamCacheInputResult(this);
        }
    }

    public class StreamCacheInputResult : IStreamCacheResult
    {
        public StreamCacheInputResult(IStreamCacheInput input)
        {
            this.Input = input;
            if (input.Bytes.Array == null) throw new ArgumentNullException("input","Input byte array null");
            Data = new MemoryStream(input.Bytes.Array, input.Bytes.Offset, input.Bytes.Count);
            
        }

        internal IStreamCacheInput Input { get; private set; }

        public Stream Data { get; private set; }
        public string ContentType => Input.ContentType;
        public string Status => "pass";
    }
}