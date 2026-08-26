using EVMTracker.TelegramGateway.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.TelegramGateway.Infrastructure.Persistence.Configurations;

internal sealed class TelegramDeliveryRecordConfiguration
    : IEntityTypeConfiguration<TelegramDeliveryRecord>
{
    public void Configure(EntityTypeBuilder<TelegramDeliveryRecord> builder)
    {
        builder.ToTable("telegram_deliveries");
        builder.HasKey(delivery => delivery.EventId);
        builder.HasIndex(delivery => delivery.NotificationId)
            .IsUnique()
            .HasDatabaseName("ux_telegram_deliveries_notification_id");
        builder.Property(delivery => delivery.Message).HasMaxLength(4096).IsRequired();
        builder.Property(delivery => delivery.ParseMode).HasMaxLength(32).IsRequired();
        builder.Property(delivery => delivery.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(delivery => delivery.LastError).HasMaxLength(2000);
        builder.HasIndex(delivery => delivery.Status)
            .HasDatabaseName("ix_telegram_deliveries_status");
    }
}
