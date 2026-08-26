using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Configurations;

internal sealed class IndexerCheckpointRecordConfiguration
    : IEntityTypeConfiguration<IndexerCheckpointRecord>
{
    public void Configure(EntityTypeBuilder<IndexerCheckpointRecord> builder)
    {
        builder.ToTable("indexer_checkpoints");
        builder.HasKey(checkpoint => checkpoint.ChainId);
        builder.Property(checkpoint => checkpoint.ChainId).ValueGeneratedNever();
    }
}
