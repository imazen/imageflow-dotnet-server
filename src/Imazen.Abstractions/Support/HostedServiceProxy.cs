using Microsoft.Extensions.Hosting;

namespace Imazen.Common.Extensibility.Support
{
    public class HostedServiceProxy<T>: IHostedService
    {   
            private readonly List<IHostedService> hostedServices;
            public HostedServiceProxy(IEnumerable<T> candidateInstances)
            {
                //filter to only IHostedService
                hostedServices = candidateInstances.Where(c => c is IHostedService).Cast<IHostedService>().ToList();
            }
            public async Task StartAsync(CancellationToken cancellationToken)
            {
                await Task.WhenAll(hostedServices.Select(c => c.StartAsync(cancellationToken)));
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                //TODO: we want errors to propagate, but we want to stop all services we can before that happens
                var tasks = hostedServices.Select(c => c.StopAsync(cancellationToken)).ToList();
                await Task.WhenAll(tasks);
            }
        
    }
}