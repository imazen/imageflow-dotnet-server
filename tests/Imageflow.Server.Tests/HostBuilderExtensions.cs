using Microsoft.Extensions.Hosting;

namespace Imageflow.Server.Tests;


internal class AsyncDisposableHost(IHost host) : IAsyncDisposable, IHost
{
    public async ValueTask DisposeAsync()
    {
        await host.StopAsync();
        host.Dispose();
    }

    public void Dispose()
    {
        host.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        return host.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = new CancellationToken())
    {
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