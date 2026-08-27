using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EVMTracker.TelegramGateway.Infrastructure.Persistence;

public sealed class TelegramGatewayDbContextDesignFactory
    : IDesignTimeDbContextFactory<TelegramGatewayDbContext>
{
    public TelegramGatewayDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__TelegramGateway") ??
            "Host=localhost;Port=5432;Database=evmtracker_telegram_gateway;" +
            "Username=postgres;Password=";
        var options = new DbContextOptionsBuilder<TelegramGatewayDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    "telegram_gateway"))
            .Options;
        return new TelegramGatewayDbContext(options);
    }
}
