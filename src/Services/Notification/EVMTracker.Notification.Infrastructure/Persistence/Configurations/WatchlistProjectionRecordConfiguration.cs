using EVMTracker.Notification.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.Notification.Infrastructure.Persistence.Configurations;

internal sealed class WatchlistProjectionRecordConfiguration
    : IEntityTypeConfiguration<WatchlistProjectionRecord>
{
    public void Configure(EntityTypeBuilder<WatchlistProjectionRecord> builder)
    {
        builder.ToTable("watchlist_projection");
        builder.HasKey(record => new { record.TrackedAddressId, record.ChainId });
        builder.Property(record => record.Address).HasMaxLength(42).IsRequired();
        builder.Property(record => record.Label).HasMaxLength(100);
        builder.HasIndex(record => new { record.ChainId, record.Address })
            .HasDatabaseName("ix_watchlist_projection_chain_address");
    }
}
