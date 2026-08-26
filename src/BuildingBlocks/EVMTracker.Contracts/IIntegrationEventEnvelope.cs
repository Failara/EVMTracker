using System.Text.Json;

namespace EVMTracker.Contracts;

public interface IIntegrationEventEnvelope
{
    Guid EventId { get; }

    string EventType { get; }

    int EventVersion { get; }

    DateTimeOffset OccurredAt { get; }

    Guid CorrelationId { get; }

    Guid? CausationId { get; }

    JsonElement Payload { get; }
}
