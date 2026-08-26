namespace EVMTracker.Notification.Infrastructure.Configuration;

public sealed class NotificationNetworkCatalogOptions
{
    public const string SectionName = "NotificationNetworks";

    public NotificationNetworkOptions[] Networks { get; init; } = [];
}

public sealed class NotificationNetworkOptions
{
    public long ChainId { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}
