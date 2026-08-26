using EVMTracker.TelegramGateway.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.TelegramGateway.Infrastructure.Persistence;

public sealed class TelegramGatewayDbContext(
    DbContextOptions<TelegramGatewayDbContext> options) : DbContext(options)
{
    internal DbSet<TelegramDeliveryRecord> TelegramDeliveries =>
        Set<TelegramDeliveryRecord>();

    internal DbSet<TelegramDeliveryAttemptRecord> TelegramDeliveryAttempts =>
        Set<TelegramDeliveryAttemptRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("telegram_gateway");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TelegramGatewayDbContext).Assembly);
    }
}
