namespace EVMTracker.Watchlist.Infrastructure.Persistence.Entities;

internal sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public int EventVersion { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid? CausationId { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public int DeliveryAttempts { get; set; }

    public string? LastError { get; set; }

    public DateTime? NextAttemptAtUtc { get; set; }

    public DateTimeOffset? DeadLetteredAtUtc { get; set; }
}
