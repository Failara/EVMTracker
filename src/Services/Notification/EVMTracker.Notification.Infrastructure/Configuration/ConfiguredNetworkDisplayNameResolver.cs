using EVMTracker.Notification.Application.Transfers;
using Microsoft.Extensions.Options;

namespace EVMTracker.Notification.Infrastructure.Configuration;

internal sealed class ConfiguredNetworkDisplayNameResolver : INetworkDisplayNameResolver
{
    private readonly Dictionary<long, string> displayNames;

    public ConfiguredNetworkDisplayNameResolver(
        IOptions<NotificationNetworkCatalogOptions> options)
    {
        displayNames = options.Value.Networks.ToDictionary(
            network => network.ChainId,
            network => network.DisplayName);
    }

    public string Resolve(long chainId) =>
        displayNames.TryGetValue(chainId, out string? displayName)
            ? displayName
            : $"EVM chain {chainId}";
}
