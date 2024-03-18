using Microsoft.Extensions.Hosting;

namespace Imageflow.Server.Tests;


internal class AsyncDisposableHost(IHost host) : IAsyncDisposable, IHost
{
    bool disposed = false;
    bool stopped = false;
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (disposed)
        {
            return;
        }
        disposed = true;
        host.Dispose();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        throw new InvalidOperationException("Use await using to dispose of this object");
    }


    public Task StartAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new InvalidOperationException("The host is already started");
    }

    public Task StopAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        if (stopped)
        {
            return Task.CompletedTask;
        }
        stopped = true;
        return host.StopAsync(cancellationToken);
    }

    public IServiceProvider Services => host.Services;
}

internal static class HostBuilderExtensions
{
    internal static async Task<AsyncDisposableHost> StartDisposableHost(this IHostBuilder hostBuilder)
    {
        var host = await hostBuilder.StartAsync();
        return new AsyncDisposableHost(host);
    }
}