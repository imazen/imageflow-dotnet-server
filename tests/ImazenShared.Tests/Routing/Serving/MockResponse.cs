using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Serving;

namespace Imazen.Routing.Tests.Serving;

[Flags]
public enum MockResponseStreamType
{
    None,
    Stream,
    Pipe,
    BufferWriter
}
/// <summary>
/// We record the final state of everything and return it as a MockResponse
/// </summary>
public class MockResponseAdapter(MockResponseStreamType enabledStreams = MockResponseStreamType.Stream | MockResponseStreamType.Pipe): IHttpResponseStreamAdapter{
    public int StatusCode { get; private set; }
    public string? ContentType => Headers.TryGetValue(HttpHeaderNames.ContentType, out var v) ? v : null;
    public long ContentLength { get; private set; }
    public bool SupportsStream => enabledStreams.HasFlag(MockResponseStreamType.Stream);
    public bool SupportsPipelines => enabledStreams.HasFlag(MockResponseStreamType.Pipe);
    public bool SupportsBufferWriter => enabledStreams.HasFlag(MockResponseStreamType.BufferWriter);
    protected Stream? BodyStream { get; private set; }
    protected PipeWriter? BodyPipeWriter { get; private set; }
    protected IBufferWriter<byte>? BodyBufferWriter { get; private set; }
    public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();
    public void SetHeader(string name, string value)
    {
        Headers[name] = value;
    }

    public void SetStatusCode(int statusCode)
    {
        StatusCode = statusCode;
    }

    public void SetContentType(string contentType)
    {
        SetHeader(HttpHeaderNames.ContentType, contentType);
    }

    public void SetContentLength(long contentLength)
    {
        ContentLength = contentLength;
    }

    public Stream GetBodyWriteStream()
    {
        if (!SupportsStream) throw new NotSupportedException();
        return BodyStream = new MemoryStream();
    }

    public PipeWriter GetBodyPipeWriter()
    {
        if (!SupportsPipelines) throw new NotSupportedException();
        return BodyPipeWriter = PipeWriter.Create(BodyStream = new MemoryStream());
    }

    public IBufferWriter<byte> GetBodyBufferWriter()
    {
        if (!SupportsBufferWriter) throw new NotSupportedException();
        BodyBufferWriter = PipeWriter.Create(BodyStream = new MemoryStream());
        return BodyBufferWriter;
    }

    public async Task<byte[]> GetBodyBytes()
    {
        if (BodyPipeWriter != null)
        {
            await BodyPipeWriter.FlushAsync();
        }
        if (BodyStream == null) return Array.Empty<byte>();
        await BodyStream.FlushAsync();
        if (BodyStream is MemoryStream ms)
        {
            return ms.ToArray();
        }

        BodyStream.Position = 0;
        var ms2 = new MemoryStream();
        await BodyStream.CopyToAsync(ms2);
        return ms2.ToArray();
    }
    
    public async Task<MockResponse> ToMockResponse()
    {
        return new MockResponse(this, await GetBodyBytes());
    }
}
    

public record MockResponse
{
    public MockResponse(MockResponseAdapter adapter, byte[] body)
    {
        StatusCode = adapter.StatusCode;
        ContentType = adapter.ContentType;
        ContentLength = adapter.ContentLength;
        Headers = adapter.Headers;
        Body = body;
    }
    public MockResponseStreamType EnabledStreams { get; init; }
    public int StatusCode { get; init; }
    public string? ContentType { get; init; }
    public long ContentLength { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public byte[] Body { get; init; }

    public string DecodeBodyUtf8()
    {
        return Encoding.UTF8.GetString(Body);
    }
}