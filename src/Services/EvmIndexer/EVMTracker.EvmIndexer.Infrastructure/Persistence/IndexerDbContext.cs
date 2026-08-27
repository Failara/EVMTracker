using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence;

public sealed class IndexerDbContext(DbContextOptions<IndexerDbContext> options)
    : DbContext(options)
{
    internal DbSet<IndexerCheckpointRecord> Checkpoints => Set<IndexerCheckpointRecord>();

    internal DbSet<ProcessedTransferRecord> ProcessedTransfers =>
        Set<ProcessedTransferRecord>();

    internal DbSet<CanonicalBlockRecord> CanonicalBlocks => Set<CanonicalBlockRecord>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("evm_indexer");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IndexerDbContext).Assembly);
    }
}
