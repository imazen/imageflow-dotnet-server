using System.Buffers;
using System.IO.Pipelines;
using Imazen.Routing.HttpAbstractions;

namespace Imazen.Routing.Serving
{
    internal static class MagicBytes
    {
        private static string GetContentTypeFromBytes(Span<byte> data)
        {
            if (data.Length < 12)
            {
                return "application/octet-stream";
            }
            return Imazen.Common.FileTypeDetection.FileTypeDetector.GuessMimeType(data) ?? "application/octet-stream";
        }

        interface IByteWritable
        {
            ValueTask Write(ArraySegment<byte> bytes, CancellationToken cancellationToken);
        }
        struct ByteWritable : IByteWritable
        {
            private readonly Stream _stream;

            public ByteWritable(Stream stream)
            {
                _stream = stream;
            }

            public ValueTask Write(ArraySegment<byte> bytes, CancellationToken cancellationToken)
            {
                if (bytes.Array == null)
                {
                    return new ValueTask();
                }
                return new System.Threading.Tasks.ValueTask(_stream.WriteAsync(bytes.Array, bytes.Offset, bytes.Count,
                    cancellationToken));
            }
        }
        struct BufferWriterWritable : IByteWritable
        {
            private readonly IBufferWriter<byte> _writer;

            public BufferWriterWritable(IBufferWriter<byte> writer)
            {
                _writer = writer;
            }

            public ValueTask Write(ArraySegment<byte> bytes, CancellationToken cancellationToken)
            {
                _writer.Write(bytes);
                return new ValueTask();
            }
        }
        struct PipeWriterWritable : IByteWritable
        {
            private readonly PipeWriter _writer;

            public PipeWriterWritable(PipeWriter writer)
            {
                _writer = writer;
            }

            public async ValueTask Write(ArraySegment<byte> bytes, CancellationToken cancellationToken)
            {
                var flushResult = await _writer.WriteAsync(bytes, cancellationToken);
                if (flushResult.IsCanceled || flushResult.IsCompleted)
                {
                    throw new OperationCanceledException();
                }
            }
        }

        
        private static IByteWritable WrapWritable<TResponse>(TResponse response) where TResponse: IHttpResponseStreamAdapter
        {
            if (response.SupportsBufferWriter)
            {
                return new BufferWriterWritable(response.GetBodyBufferWriter());
            }
            else if (response.SupportsStream)
            {
                return new ByteWritable(response.GetBodyWriteStream());
            }
            else if (response.SupportsPipelines)
            {
                return new PipeWriterWritable(response.GetBodyPipeWriter());
            }
            else
            {
                throw new InvalidOperationException("Response does not support writing");
            }
        }

        /// <summary>
        /// Proxies the given stream to the HTTP response, while also setting the content length
        /// and the content type based off the magic bytes of the image
        /// 
        /// TODO: use pipelines
        /// </summary>
        /// <param name="sourceStream"></param>
        /// <param name="response"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="InvalidOperationException"></exception>
        internal static async Task ProxyToStream<TResponse>(Stream sourceStream, TResponse response, CancellationToken cancellationToken) where TResponse: IHttpResponseStreamAdapter
        {
            if (sourceStream.CanSeek)
            {
                response.SetContentLength(sourceStream.Length - sourceStream.Position);
            }
#if DOTNET5_0_OR_GREATER
            if (response.SupportsPipelines)
            {
                var writer = response.GetBodyPipeWriter();
                
                // read the first 12 of 4096 bytes to determine the content type
                var memory = writer.GetMemory(4096);
                var bytesRead = await sourceStream.ReadAsync(memory, cancellationToken).ConfigureAwait(false);
                response.SetContentType(bytesRead >= 12
                    ? GetContentTypeFromBytes(memory.Span.Slice(0, bytesRead))
                    : "application/octet-stream");
                writer.Advance(bytesRead);
                await sourceStream.CopyToAsync(writer, cancellationToken).ConfigureAwait(false);
                writer.Complete();
                return;
            }
#endif
            if (response.SupportsStream)
            {
                var responseStream = response.GetBodyWriteStream();
                // We really only need 12 bytes but it would be a waste to only read that many. 
                const int bufferSize = 4096;
                var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                try
                {
                    var bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                        .ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        throw new InvalidOperationException("Source blob has zero bytes.");
                    }

                    response.SetContentType(bytesRead >= 12
                        ? GetContentTypeFromBytes(buffer)
                        : "application/octet-stream");
                    await responseStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                await sourceStream.CopyToAsync(responseStream, 81920, cancellationToken).ConfigureAwait(false);
                return;
            }
            else
            {
                throw new InvalidOperationException("Response does not support writing to stream");
            }

        }
    }
}