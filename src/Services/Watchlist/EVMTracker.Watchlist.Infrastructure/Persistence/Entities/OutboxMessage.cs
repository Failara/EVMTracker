namespace EVMTracker.Watchlist.Infrastructure.Persistence.Entities;

internal sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public int DeliveryAttempts { get; set; }

    public string? LastError { get; set; }
}
