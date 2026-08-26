namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;

internal sealed class IndexerCheckpointRecord
{
    public long ChainId { get; set; }

    public long NextBlockNumber { get; set; }

    public int CurrentRangeSize { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
