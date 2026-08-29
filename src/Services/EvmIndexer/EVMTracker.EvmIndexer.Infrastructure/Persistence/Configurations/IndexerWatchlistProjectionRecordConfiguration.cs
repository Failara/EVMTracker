using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Configurations;

internal sealed class IndexerWatchlistProjectionRecordConfiguration
    : IEntityTypeConfiguration<IndexerWatchlistProjectionRecord>
{
    public void Configure(EntityTypeBuilder<IndexerWatchlistProjectionRecord> builder)
    {
        builder.ToTable("watchlist_projection");
        builder.HasKey(record => new { record.TrackedAddressId, record.ChainId });
        builder.Property(record => record.Address).HasMaxLength(42).IsRequired();
        builder.HasIndex(record => new { record.ChainId, record.Address })
            .HasDatabaseName("ix_indexer_watchlist_projection_chain_address");
    }
}
