using System.Text.Json;

namespace EVMTracker.Contracts.Notifications;

public sealed record NotificationIntegrationEventEnvelope(
    Guid EventId,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAt,
    Guid CorrelationId,
    Guid? CausationId,
    JsonElement Payload) : IIntegrationEventEnvelope;
