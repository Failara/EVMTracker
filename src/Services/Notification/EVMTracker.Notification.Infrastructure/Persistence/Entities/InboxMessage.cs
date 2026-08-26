namespace EVMTracker.Notification.Infrastructure.Persistence.Entities;

internal sealed class InboxMessage
{
    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; set; }
}
