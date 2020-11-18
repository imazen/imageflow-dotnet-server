using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Imazen.Common.Issues;
using Microsoft.Extensions.Hosting;

namespace Imazen.Common.Extensibility.StreamCache
{
    public delegate Task<string> AsyncStreamWriteResult(Stream output);

    public delegate Task<KeyValuePair<string,ArraySegment<byte>>> AsyncBytesResult();


    public interface IStreamCache : IIssueProvider, IHostedService
    {
        Task<IStreamCacheResult> GetOrCreateStream(byte[] key, AsyncStreamWriteResult writeCallback);
        Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult writeCallback);
    }
}