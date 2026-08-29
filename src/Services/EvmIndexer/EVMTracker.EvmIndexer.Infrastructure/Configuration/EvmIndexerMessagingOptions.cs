namespace EVMTracker.EvmIndexer.Infrastructure.Configuration;

public sealed class EvmIndexerMessagingOptions
{
    public const string SectionName = "EvmIndexerMessaging";

    public string WatchlistProjectionQueue { get; init; } =
        "indexer-watchlist-projection";

    public ushort PrefetchCount { get; init; } = 16;

    public TimeSpan[] RetryIntervals { get; init; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];
}
