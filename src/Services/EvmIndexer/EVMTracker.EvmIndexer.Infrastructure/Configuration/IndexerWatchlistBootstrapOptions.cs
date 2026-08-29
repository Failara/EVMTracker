namespace EVMTracker.EvmIndexer.Infrastructure.Configuration;

public sealed class IndexerWatchlistBootstrapOptions
{
    public const string SectionName = "IndexerWatchlistBootstrap";

    public Uri BaseUrl { get; init; } = new("http://localhost:5150");

    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromSeconds(5);
}
