using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EVMTracker.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContextDesignFactory
    : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=evmtracker_notification;Username=postgres;Password=postgres",
                postgres => postgres.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    "notification"))
            .Options;
        return new NotificationDbContext(options);
    }
}
