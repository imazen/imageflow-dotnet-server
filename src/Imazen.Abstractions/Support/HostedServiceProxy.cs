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
            public Task StartAsync(CancellationToken cancellationToken)
            {
                return Task.WhenAll(hostedServices.Select(c => c.StartAsync(cancellationToken)));
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                //TODO: we want errors to propagate, but we want to stop all services we can before that happens
                var tasks = hostedServices.Select(c => c.StopAsync(cancellationToken)).ToList();
                return Task.WhenAll(tasks);
            }
        
    }
}