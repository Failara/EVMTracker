using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EVMTracker.Watchlist.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWatchlistApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly));
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
