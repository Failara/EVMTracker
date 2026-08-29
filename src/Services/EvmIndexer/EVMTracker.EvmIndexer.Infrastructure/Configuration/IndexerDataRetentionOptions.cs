namespace EVMTracker.EvmIndexer.Infrastructure.Configuration;

public sealed class IndexerDataRetentionOptions
{
    public const string SectionName = "IndexerDataRetention";

    public bool Enabled { get; init; } = true;

    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan PublishedOutboxRetention { get; init; } = TimeSpan.FromDays(1);

    public TimeSpan TransferHistoryRetention { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan WatchlistInboxRetention { get; init; } = TimeSpan.FromDays(30);

    public int BatchSize { get; init; } = 1_000;
}
