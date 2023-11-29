using System.Collections.Concurrent;
using Imazen.Abstractions.BlobCache;
using Imazen.Abstractions.Logging;
using Imazen.Routing.Helpers;
using Microsoft.Extensions.Hosting;

namespace Imazen.Routing.Health;

internal class CacheHealthTracker(IReLogger logger) : IHostedService
{
    private readonly ConcurrentDictionary<IBlobCache, CacheHealthStatus> cacheHealth = new ConcurrentDictionary<IBlobCache, CacheHealthStatus>();
    
    // ReSharper disable once HeapView.CanAvoidClosure
    private CacheHealthStatus this[IBlobCache cache] => cacheHealth.GetOrAdd(cache, c => new CacheHealthStatus(c, logger));
    
    public BlobCacheCapabilities GetEstimatedCapabilities(IBlobCache cache)
    {
        return this[cache].GetEstimatedCapabilities();
    }
    
    public ValueTask<IBlobCacheHealthDetails?> CheckHealth(IBlobCache cache, bool forceFresh, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return cacheHealth[cache].CheckHealth(forceFresh, timeout,  cancellationToken);
    }
    
    public async ValueTask<string> GetReport(bool forceFresh, CancellationToken cancellationToken = default)
    {
        var reports = await Tasks.WhenAll(cacheHealth.Values.Select(x => x.GetReport(forceFresh, cancellationToken)));
        return string.Join("\n\n", reports);
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(cacheHealth.Values.Select(x => x.StopAsync(cancellationToken)).ToArray());
    }
}