using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EVMTracker.TelegramGateway.Infrastructure.Persistence;

public sealed class TelegramGatewayDbContextDesignFactory
    : IDesignTimeDbContextFactory<TelegramGatewayDbContext>
{
    public TelegramGatewayDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TelegramGatewayDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=evmtracker_telegram_gateway;Username=postgres;Password=postgres",
                postgres => postgres.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    "telegram_gateway"))
            .Options;
        return new TelegramGatewayDbContext(options);
    }
}
