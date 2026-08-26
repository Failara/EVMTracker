using EVMTracker.Watchlist.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.Watchlist.Infrastructure.Persistence.Configurations;

internal sealed class TrackedAddressRecordConfiguration
    : IEntityTypeConfiguration<TrackedAddressRecord>
{
    public void Configure(EntityTypeBuilder<TrackedAddressRecord> builder)
    {
        builder.ToTable("tracked_addresses");
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Address)
            .HasMaxLength(42)
            .IsRequired();
        builder.Property(record => record.Label)
            .HasMaxLength(64);
        builder.Property(record => record.TelegramUserId)
            .IsRequired();
        builder.Property(record => record.TelegramChatId)
            .IsRequired();
        builder.Property(record => record.IsActive)
            .IsRequired();
        builder.Property(record => record.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(record => record.TelegramUserId)
            .HasDatabaseName("ix_tracked_addresses_telegram_user_id");

        builder.HasMany(record => record.Networks)
            .WithOne(network => network.TrackedAddress)
            .HasForeignKey(network => network.TrackedAddressId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
