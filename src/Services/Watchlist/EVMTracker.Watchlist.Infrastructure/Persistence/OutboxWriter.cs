using System.Text.Json;
using EVMTracker.Contracts.Watchlists;
using EVMTracker.Watchlist.Application.Abstractions.Messaging;
using EVMTracker.Watchlist.Infrastructure.Persistence.Entities;

namespace EVMTracker.Watchlist.Infrastructure.Persistence;

internal sealed class OutboxWriter(WatchlistDbContext dbContext) : IOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Add(IWatchlistIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        dbContext.OutboxMessages.Add(
            new OutboxMessage
            {
                Id = integrationEvent.EventId,
                EventType = integrationEvent.EventType,
                OccurredAtUtc = integrationEvent.OccurredAtUtc,
                Payload = JsonSerializer.Serialize(
                    integrationEvent,
                    integrationEvent.GetType(),
                    SerializerOptions),
            });
    }
}
