using EVMTracker.Notification.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.Notification.Infrastructure.Persistence.Configurations;

internal sealed class TransferNotificationRecordConfiguration
    : IEntityTypeConfiguration<TransferNotificationRecord>
{
    public const string UniqueLogicalNotificationConstraintName =
        "ux_transfer_notifications_subscription_chain_transaction_log";

    public void Configure(EntityTypeBuilder<TransferNotificationRecord> builder)
    {
        builder.ToTable("transfer_notifications");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.NetworkName).HasMaxLength(100).IsRequired();
        builder.Property(record => record.MonitoredAddress).HasMaxLength(42).IsRequired();
        builder.Property(record => record.Label).HasMaxLength(100);
        builder.Property(record => record.Direction).HasConversion<string>().HasMaxLength(16);
        builder.Property(record => record.TokenContract).HasMaxLength(42).IsRequired();
        builder.Property(record => record.TokenSymbol).HasMaxLength(64).IsRequired();
        builder.Property(record => record.TokenName).HasMaxLength(256);
        builder.Property(record => record.RawValue).HasMaxLength(78).IsRequired();
        builder.Property(record => record.DisplayAmount).HasMaxLength(336).IsRequired();
        builder.Property(record => record.FromAddress).HasMaxLength(42).IsRequired();
        builder.Property(record => record.ToAddress).HasMaxLength(42).IsRequired();
        builder.Property(record => record.TransactionHash).HasMaxLength(66).IsRequired();
        builder.Property(record => record.BlockHash).HasMaxLength(66).IsRequired();
        builder.HasIndex(
                record => new
                {
                    record.SubscriptionId,
                    record.ChainId,
                    record.TransactionHash,
                    record.LogIndex,
                })
            .IsUnique()
            .HasDatabaseName(UniqueLogicalNotificationConstraintName);
        builder.HasIndex(record => record.SourceEventId)
            .HasDatabaseName("ix_transfer_notifications_source_event_id");
    }
}
