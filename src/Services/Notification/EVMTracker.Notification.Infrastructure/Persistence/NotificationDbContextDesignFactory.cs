using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EVMTracker.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContextDesignFactory
    : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__Notification") ??
            "Host=localhost;Port=5432;Database=evmtracker_notification;" +
            "Username=postgres;Password=";
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    "notification"))
            .Options;
        return new NotificationDbContext(options);
    }
}
