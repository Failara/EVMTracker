using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Configurations;

internal sealed class IndexerWatchlistInboxRecordConfiguration
    : IEntityTypeConfiguration<IndexerWatchlistInboxRecord>
{
    public void Configure(EntityTypeBuilder<IndexerWatchlistInboxRecord> builder)
    {
        builder.ToTable("watchlist_inbox_messages");
        builder.HasKey(record => record.EventId);
        builder.Property(record => record.EventType).HasMaxLength(200).IsRequired();
    }
}
