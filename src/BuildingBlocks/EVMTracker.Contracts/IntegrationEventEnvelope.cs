using System.Text.Json;

namespace EVMTracker.Contracts;

public sealed record IntegrationEventEnvelope(
    Guid EventId,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAt,
    Guid CorrelationId,
    Guid? CausationId,
    JsonElement Payload) : IIntegrationEventEnvelope;
