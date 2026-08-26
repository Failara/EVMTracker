namespace EVMTracker.Messaging;

public sealed record OutboxDispatchItem(
    Guid Id,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid? CausationId,
    string Payload,
    int DeliveryAttempts);
