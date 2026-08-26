using System.Text.Json;
using EVMTracker.Contracts;
using EVMTracker.Contracts.Watchlists;

namespace EVMTracker.Notification.Application.Watchlists;

public sealed class WatchlistProjectionProjector(IWatchlistProjectionStore store)
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public Task ProjectAsync(
        WatchlistIntegrationEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.EventVersion != 1)
        {
            throw new UnsupportedWatchlistEventException(
                $"Watchlist event '{envelope.EventType}' version {envelope.EventVersion} is unsupported.");
        }

        return envelope.EventType switch
        {
            WatchlistEventTypes.AddressAddedV1 => store.ApplyAddedAsync(
                envelope.EventId,
                envelope.OccurredAt,
                Deserialize<AddressAddedV1>(envelope),
                cancellationToken),
            WatchlistEventTypes.AddressUpdatedV1 => store.ApplyUpdatedAsync(
                envelope.EventId,
                envelope.OccurredAt,
                Deserialize<AddressUpdatedV1>(envelope),
                cancellationToken),
            WatchlistEventTypes.AddressRemovedV1 => store.ApplyRemovedAsync(
                envelope.EventId,
                envelope.OccurredAt,
                Deserialize<AddressRemovedV1>(envelope),
                cancellationToken),
            WatchlistEventTypes.ProjectionResetV1 => store.ResetAsync(
                envelope.EventId,
                envelope.OccurredAt,
                Deserialize<WatchlistProjectionResetV1>(envelope),
                cancellationToken),
            _ => throw new UnsupportedWatchlistEventException(
                $"Watchlist event type '{envelope.EventType}' is unsupported."),
        };
    }

    private static TEvent Deserialize<TEvent>(WatchlistIntegrationEventEnvelope envelope)
        where TEvent : IIntegrationEvent
    {
        TEvent? integrationEvent = envelope.Payload.Deserialize<TEvent>(SerializerOptions);
        if (integrationEvent is null ||
            integrationEvent.EventId != envelope.EventId ||
            integrationEvent.EventType != envelope.EventType ||
            integrationEvent.EventVersion != envelope.EventVersion)
        {
            throw new JsonException(
                $"The payload metadata for '{envelope.EventType}' does not match its envelope.");
        }

        return integrationEvent;
    }
}
