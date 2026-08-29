namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;

internal sealed class IndexerWatchlistInboxRecord
{
    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; set; }
}
