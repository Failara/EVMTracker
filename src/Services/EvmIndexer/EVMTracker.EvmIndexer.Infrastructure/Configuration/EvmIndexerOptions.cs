namespace EVMTracker.EvmIndexer.Infrastructure.Configuration;

public sealed class EvmIndexerOptions
{
    public const string SectionName = "EvmIndexer";

    public TimeSpan RpcRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public List<EvmNetworkOptions> Networks { get; set; } = [];
}

public sealed class EvmNetworkOptions
{
    public string Name { get; set; } = string.Empty;

    public long ChainId { get; set; }

    public string RpcUrl { get; set; } = string.Empty;

    public long StartBlock { get; set; }

    public int ConfirmationDepth { get; set; } = 12;

    public int InitialBlockRange { get; set; } = 1_000;

    public int MinimumBlockRange { get; set; } = 10;

    public int MaximumBlockRange { get; set; } = 10_000;

    public int ReorganizationWindow { get; set; } = 64;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
}
