using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedTransferRecordConfiguration
    : IEntityTypeConfiguration<ProcessedTransferRecord>
{
    public const string UniqueTransferConstraintName =
        "ux_processed_transfers_chain_transaction_log";

    public void Configure(EntityTypeBuilder<ProcessedTransferRecord> builder)
    {
        builder.ToTable("processed_transfers");
        builder.HasKey(transfer => transfer.Id);
        builder.HasIndex(transfer => transfer.ObservedEventId)
            .IsUnique()
            .HasDatabaseName("ux_processed_transfers_observed_event_id");
        builder.Property(transfer => transfer.TransactionHash).HasMaxLength(66).IsRequired();
        builder.Property(transfer => transfer.BlockHash).HasMaxLength(66).IsRequired();
        builder.Property(transfer => transfer.TokenContract).HasMaxLength(42).IsRequired();
        builder.Property(transfer => transfer.FromAddress).HasMaxLength(42).IsRequired();
        builder.Property(transfer => transfer.ToAddress).HasMaxLength(42).IsRequired();
        builder.Property(transfer => transfer.RawValue).HasMaxLength(78).IsRequired();
        builder.Property(transfer => transfer.TokenSymbol).HasMaxLength(64).IsRequired();
        builder.Property(transfer => transfer.TokenName).HasMaxLength(256);
        builder.HasIndex(
                transfer => new
                {
                    transfer.ChainId,
                    transfer.TransactionHash,
                    transfer.LogIndex,
                })
            .IsUnique()
            .HasDatabaseName(UniqueTransferConstraintName);
        builder.HasIndex(transfer => new { transfer.ChainId, transfer.BlockNumber });
        builder.HasIndex(transfer => new { transfer.ChainId, transfer.IsOrphaned });
    }
}
