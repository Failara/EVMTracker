namespace EVMTracker.Notification.Infrastructure.Configuration;

public sealed class NotificationMessagingOptions
{
    public const string SectionName = "NotificationMessaging";

    public string WatchlistProjectionQueue { get; init; } =
        "notification-watchlist-projection";

    public string TransferMatcherQueue { get; init; } =
        "notification-transfer-matcher";

    public ushort PrefetchCount { get; init; } = 16;

    public TimeSpan[] RetryIntervals { get; init; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];
}
