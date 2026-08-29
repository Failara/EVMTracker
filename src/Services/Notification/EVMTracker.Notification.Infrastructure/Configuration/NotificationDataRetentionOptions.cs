namespace EVMTracker.Notification.Infrastructure.Configuration;

public sealed class NotificationDataRetentionOptions
{
    public const string SectionName = "NotificationDataRetention";

    public bool Enabled { get; init; } = true;

    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan PublishedOutboxRetention { get; init; } = TimeSpan.FromDays(1);

    public TimeSpan InboxRetention { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan NotificationHistoryRetention { get; init; } = TimeSpan.FromDays(30);

    public int BatchSize { get; init; } = 1_000;
}
