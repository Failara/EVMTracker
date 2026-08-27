using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Configurations;

internal sealed class CanonicalBlockRecordConfiguration
    : IEntityTypeConfiguration<CanonicalBlockRecord>
{
    public void Configure(EntityTypeBuilder<CanonicalBlockRecord> builder)
    {
        builder.ToTable("canonical_blocks");
        builder.HasKey(block => new { block.ChainId, block.BlockNumber });
        builder.Property(block => block.BlockHash).HasMaxLength(66).IsRequired();
        builder.HasIndex(block => new { block.ChainId, block.BlockHash })
            .HasDatabaseName("ix_canonical_blocks_chain_hash");
    }
}
