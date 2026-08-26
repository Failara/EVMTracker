using EVMTracker.Notification.Application.Transfers;
using Microsoft.Extensions.Options;

namespace EVMTracker.Notification.Infrastructure.Configuration;

internal sealed class ConfiguredNetworkPresentationResolver : INetworkPresentationResolver
{
    private readonly Dictionary<long, NetworkPresentation> networks;

    public ConfiguredNetworkPresentationResolver(
        IOptions<NotificationNetworkCatalogOptions> options)
    {
        networks = options.Value.Networks.ToDictionary(
            network => network.ChainId,
            network => new NetworkPresentation(
                network.DisplayName,
                network.ExplorerBaseUrl));
    }

    public NetworkPresentation Resolve(long chainId) =>
        networks.TryGetValue(chainId, out NetworkPresentation? network)
            ? network
            : new NetworkPresentation($"EVM chain {chainId}", null);
}
