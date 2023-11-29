using Imazen.Routing.Caching.Health;
using Microsoft.Extensions.Hosting;

namespace Imazen.Routing.Unused;

internal class CachedNonOverlappingRunner<T>(NonOverlappingAsyncRunner<T> runner, TimeSpan staleAfter)
    : IHostedService, IDisposable
{
    public void Dispose()
    {
        runner.Dispose();
    }

    public T? LastResult { get; private set; }
    public DateTimeOffset? LastRun { get; private set; }
    public TimeSpan? GetTimeSinceLastRun() => LastRun.HasValue ? DateTimeOffset.UtcNow - LastRun : null;

    public async ValueTask<T?> GetResultAsync(bool forceFresh, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return LastResult!;
        if (!forceFresh && LastRun.HasValue && GetTimeSinceLastRun() < staleAfter) return LastResult!;
        var result = await runner.RunNonOverlappingAsync(timeout, cancellationToken);
        LastResult = result;
        LastRun = DateTimeOffset.UtcNow;
        return result;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return runner.StopAsync(cancellationToken);
    }
}
    
    
    
    
    
   
    
    
    
    
    