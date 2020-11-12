using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Imageflow.Server
{
    internal static class MagicBytes
    {
        internal static string GetContentTypeFromBytes(byte[] data)
        {
            if (data.Length < 12)
            {
                return "application/octet-stream";
            }
            return Fluent.ImageJob.GetContentTypeForBytes(data) ?? "application/octet-stream";
        }
        
        
        /// <summary>
        /// Proxies the given stream to the HTTP response, while also setting the content length
        /// and the content type based off the magic bytes of the image
        /// </summary>
        /// <param name="sourceStream"></param>
        /// <param name="response"></param>
        /// <exception cref="InvalidOperationException"></exception>
        internal static async Task ProxyToStream(Stream sourceStream, HttpResponse response)
        {
            if (sourceStream.CanSeek)
            {
                response.ContentLength = sourceStream.Length - sourceStream.Position;
            }
            
            // We really only need 12 bytes but it would be a waste to only read that many. 
            var bufferSize = 4096;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                int bytesRead = await sourceStream.ReadAsync(new Memory<byte>(buffer)).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException("Source blob has zero bytes.");
                }
                
                response.ContentType = bytesRead >= 12 ? GetContentTypeFromBytes(buffer) : "application/octet-stream";
                await response.Body.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead)).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
            await sourceStream.CopyToAsync(response.Body).ConfigureAwait(false);
        }
    }
}