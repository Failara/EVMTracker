using System.Text.Json;
using EVMTracker.Contracts;

namespace EVMTracker.Messaging.Publishing;

public static class IntegrationEventEnvelopeFactory
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static IntegrationEventEnvelope Create(
        IIntegrationEvent integrationEvent,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        JsonElement payload = JsonSerializer.SerializeToElement(
            integrationEvent,
            integrationEvent.GetType(),
            SerializerOptions);
        return new IntegrationEventEnvelope(
            integrationEvent.EventId,
            integrationEvent.EventType,
            integrationEvent.EventVersion,
            integrationEvent.OccurredAtUtc,
            correlationId ?? integrationEvent.EventId,
            causationId,
            payload);
    }

    public static IntegrationEventEnvelope Create(OutboxDispatchItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        using JsonDocument document = JsonDocument.Parse(item.Payload);
        return new IntegrationEventEnvelope(
            item.Id,
            item.EventType,
            item.EventVersion,
            item.OccurredAtUtc,
            item.CorrelationId,
            item.CausationId,
            document.RootElement.Clone());
    }
}
