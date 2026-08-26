using System.Text.Json;

namespace EVMTracker.Contracts.Watchlists;

public sealed record WatchlistIntegrationEventEnvelope(
    Guid EventId,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAt,
    Guid CorrelationId,
    Guid? CausationId,
    JsonElement Payload) : IIntegrationEventEnvelope;
