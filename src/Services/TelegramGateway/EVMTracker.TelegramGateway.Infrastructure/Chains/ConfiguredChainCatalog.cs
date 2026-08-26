using EVMTracker.TelegramGateway.Application.Abstractions;

namespace EVMTracker.TelegramGateway.Infrastructure.Chains;

public sealed class ConfiguredChainCatalog : IChainCatalog
{
    private readonly IReadOnlyDictionary<string, ChainNetwork> networksByName;

    public ConfiguredChainCatalog(IEnumerable<ChainNetwork> networks)
    {
        ArgumentNullException.ThrowIfNull(networks);

        ChainNetwork[] configuredNetworks = networks.ToArray();
        Networks = configuredNetworks;
        networksByName = configuredNetworks.ToDictionary(
            network => network.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<ChainNetwork> Networks { get; }

    public ChainNetwork? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return networksByName.GetValueOrDefault(name.Trim());
    }
}
