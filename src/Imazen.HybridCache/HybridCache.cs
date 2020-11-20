using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Issues;

namespace Imazen.HybridCache
{
    public class HybridCache : IStreamCache
    {
        public IEnumerable<IIssue> GetIssues()
        {
            throw new System.NotImplementedException();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
        
        public Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult writeCallback, CancellationToken cancellationToken, bool retrieveContentType)
        {
            throw new System.NotImplementedException();
        }
    }
}