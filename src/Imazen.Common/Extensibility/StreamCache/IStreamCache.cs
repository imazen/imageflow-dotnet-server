using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Issues;
using Microsoft.Extensions.Hosting;

namespace Imazen.Common.Extensibility.StreamCache
{
    public delegate Task<Tuple<string,ArraySegment<byte>>> AsyncBytesResult(CancellationToken cancellationToken);
    
    public interface IStreamCache : IIssueProvider, IHostedService
    {
        Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult writeCallback, CancellationToken cancellationToken, bool retrieveContentType);
    }
}