using System.Text.Json;

namespace EVMTracker.Contracts.Transfers;

public sealed record TransferIntegrationEventEnvelope(
    Guid EventId,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAt,
    Guid CorrelationId,
    Guid? CausationId,
    JsonElement Payload) : IIntegrationEventEnvelope;
