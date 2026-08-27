namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;

internal sealed class CanonicalBlockRecord
{
    public long ChainId { get; set; }

    public long BlockNumber { get; set; }

    public string BlockHash { get; set; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; set; }
}
