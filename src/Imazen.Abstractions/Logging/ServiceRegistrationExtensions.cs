using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imazen.Abstractions.Logging;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddImageflowReLogStoreAndReLoggerFactoryIfMissing(this IServiceCollection services,
        ReLogStoreOptions? logStorageOptions = null)
    {
        // Only add if not already added
        if (services.All(x => x.ServiceType != typeof(IReLogStore)))
        {
            services.AddSingleton<IReLogStore>((container) => new ReLogStore(logStorageOptions ?? new ReLogStoreOptions()));
        }
        if (services.All(x => x.ServiceType != typeof(IReLoggerFactory)))
        {
            services.AddSingleton<IReLoggerFactory>(container =>
                new ReLoggerFactory(container.GetRequiredService<ILoggerFactory>(),
                    container.GetRequiredService<IReLogStore>()));
        }
        return services;
    }

}