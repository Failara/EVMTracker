using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EVMTracker.Watchlist.Infrastructure.Persistence;

public sealed class WatchlistDbContextFactory
    : IDesignTimeDbContextFactory<WatchlistDbContext>
{
    public WatchlistDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WatchlistDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=evmtracker_watchlist;" +
            "Username=postgres;Password=",
            options => options.MigrationsHistoryTable("__ef_migrations_history", "watchlist"));

        return new WatchlistDbContext(optionsBuilder.Options);
    }
}
