using EVMTracker.Watchlist.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.Watchlist.Infrastructure.Persistence.Configurations;

internal sealed class TrackedAddressNetworkRecordConfiguration
    : IEntityTypeConfiguration<TrackedAddressNetworkRecord>
{
    public const string UniqueTrackingConstraintName =
        "ux_tracked_address_networks_user_address_chain";

    public void Configure(EntityTypeBuilder<TrackedAddressNetworkRecord> builder)
    {
        builder.ToTable("tracked_address_networks");
        builder.HasKey(record => new { record.TrackedAddressId, record.ChainId });

        builder.Property(record => record.Address)
            .HasMaxLength(42)
            .IsRequired();
        builder.Property(record => record.TelegramUserId)
            .IsRequired();
        builder.Property(record => record.ChainId)
            .IsRequired();

        builder.HasIndex(record => new
        {
            record.TelegramUserId,
            record.Address,
            record.ChainId,
        })
            .IsUnique()
            .HasDatabaseName(UniqueTrackingConstraintName);
    }
}
