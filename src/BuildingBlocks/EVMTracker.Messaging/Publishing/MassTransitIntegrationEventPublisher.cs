using EVMTracker.Contracts;
using EVMTracker.Contracts.Notifications;
using EVMTracker.Contracts.Transfers;
using EVMTracker.Contracts.Watchlists;
using MassTransit;

namespace EVMTracker.Messaging.Publishing;

internal sealed class MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint)
    : IIntegrationEventPublisher
{
    public Task PublishAsync(
        IntegrationEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        object categoryEnvelope = envelope.EventType switch
        {
            string eventType when eventType.StartsWith("watchlist.", StringComparison.Ordinal) =>
                new WatchlistIntegrationEventEnvelope(
                    envelope.EventId,
                    envelope.EventType,
                    envelope.EventVersion,
                    envelope.OccurredAt,
                    envelope.CorrelationId,
                    envelope.CausationId,
                    envelope.Payload),
            string eventType when eventType.StartsWith("transfers.", StringComparison.Ordinal) =>
                new TransferIntegrationEventEnvelope(
                    envelope.EventId,
                    envelope.EventType,
                    envelope.EventVersion,
                    envelope.OccurredAt,
                    envelope.CorrelationId,
                    envelope.CausationId,
                    envelope.Payload),
            string eventType when eventType.StartsWith("notifications.", StringComparison.Ordinal) =>
                new NotificationIntegrationEventEnvelope(
                    envelope.EventId,
                    envelope.EventType,
                    envelope.EventVersion,
                    envelope.OccurredAt,
                    envelope.CorrelationId,
                    envelope.CausationId,
                    envelope.Payload),
            _ => throw new InvalidOperationException(
                $"No integration-event category is registered for '{envelope.EventType}'."),
        };
        return publishEndpoint.Publish(categoryEnvelope, categoryEnvelope.GetType(), cancellationToken);
    }
}
