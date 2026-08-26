using EVMTracker.Watchlist.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.Watchlist.Infrastructure.Persistence;

public sealed class WatchlistDbContext(DbContextOptions<WatchlistDbContext> options)
    : DbContext(options)
{
    internal DbSet<TrackedAddressRecord> TrackedAddresses => Set<TrackedAddressRecord>();

    internal DbSet<TrackedAddressNetworkRecord> TrackedAddressNetworks =>
        Set<TrackedAddressNetworkRecord>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("watchlist");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WatchlistDbContext).Assembly);
    }
}
