using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Issues;
using Microsoft.Extensions.Hosting;

namespace Imazen.Common.Extensibility.StreamCache
{
    // A tuple 
    public delegate Task<IStreamCacheInput> AsyncBytesResult(CancellationToken cancellationToken);
    
    public interface IStreamCache : IIssueProvider, IHostedService
    {
        Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult dataProviderCallback, CancellationToken cancellationToken, bool retrieveContentType);
    }
}