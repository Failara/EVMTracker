using EVMTracker.Notification.Application.Watchlists;
using Microsoft.Extensions.DependencyInjection;

namespace EVMTracker.Notification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        services.AddScoped<WatchlistProjectionProjector>();
        return services;
    }
}
