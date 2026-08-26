using EVMTracker.Watchlist.Application.Abstractions.Messaging;
using EVMTracker.Watchlist.Application.Abstractions.Persistence;
using EVMTracker.Watchlist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EVMTracker.Watchlist.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWatchlistInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<WatchlistDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                postgresOptions => postgresOptions.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    "watchlist")));
        services.AddScoped<ITrackedAddressRepository, TrackedAddressRepository>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IWatchlistUnitOfWork, WatchlistUnitOfWork>();

        return services;
    }
}
