using EVMTracker.Notification.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.Notification.Infrastructure.Persistence.Configurations;

internal sealed class OrphanedTransferRecordConfiguration
    : IEntityTypeConfiguration<OrphanedTransferRecord>
{
    public const string UniqueOrphanConstraintName =
        "ux_orphaned_transfers_chain_transaction_log_block";

    public void Configure(EntityTypeBuilder<OrphanedTransferRecord> builder)
    {
        builder.ToTable("orphaned_transfers");
        builder.HasKey(orphan => orphan.EventId);
        builder.Property(orphan => orphan.TransactionHash).HasMaxLength(66).IsRequired();
        builder.Property(orphan => orphan.FormerBlockHash).HasMaxLength(66).IsRequired();
        builder.HasIndex(orphan => new
        {
            orphan.ChainId,
            orphan.TransactionHash,
            orphan.LogIndex,
            orphan.FormerBlockHash,
        })
            .IsUnique()
            .HasDatabaseName(UniqueOrphanConstraintName);
    }
}
