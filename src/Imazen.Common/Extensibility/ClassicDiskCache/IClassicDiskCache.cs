using Imazen.Common.Issues;
using Microsoft.Extensions.Hosting;

namespace Imazen.Common.Extensibility.ClassicDiskCache
{
    [Obsolete("Implement IBlobCacheProvider and IBlobCache instead")]
    public interface IClassicDiskCache: IIssueProvider, IHostedService
    {
        Task<ICacheResult> GetOrCreate(string key, string fileExtension, AsyncWriteResult writeCallback);
    }
}