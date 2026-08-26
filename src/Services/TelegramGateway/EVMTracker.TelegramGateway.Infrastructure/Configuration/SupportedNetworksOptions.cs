namespace EVMTracker.TelegramGateway.Infrastructure.Configuration;

public sealed class SupportedNetworksOptions
{
    public const string SectionName = "SupportedNetworks";

    public List<SupportedNetworkOptions> Networks { get; set; } = [];
}

public sealed class SupportedNetworkOptions
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public long ChainId { get; set; }
}
