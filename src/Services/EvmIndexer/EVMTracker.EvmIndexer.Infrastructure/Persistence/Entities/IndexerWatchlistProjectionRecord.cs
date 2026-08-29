namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;

internal sealed class IndexerWatchlistProjectionRecord
{
    public Guid TrackedAddressId { get; set; }

    public long ChainId { get; set; }

    public string Address { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
