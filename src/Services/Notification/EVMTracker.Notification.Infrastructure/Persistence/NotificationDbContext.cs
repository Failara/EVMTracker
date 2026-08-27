using EVMTracker.Notification.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : DbContext(options)
{
    internal DbSet<WatchlistProjectionRecord> WatchlistProjection =>
        Set<WatchlistProjectionRecord>();

    internal DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    internal DbSet<TransferNotificationRecord> TransferNotifications =>
        Set<TransferNotificationRecord>();

    internal DbSet<OrphanedTransferRecord> OrphanedTransfers =>
        Set<OrphanedTransferRecord>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("notification");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
    }
}
