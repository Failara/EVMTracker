using EVMTracker.Contracts.Watchlists;
using EVMTracker.Messaging;
using EVMTracker.Messaging.Publishing;
using EVMTracker.Watchlist.Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.Watchlist.Infrastructure.Persistence;

internal sealed class WatchlistEventReplayService(
    WatchlistDbContext dbContext,
    IIntegrationEventPublisher publisher,
    TimeProvider timeProvider) : IWatchlistEventReplayService
{
    public async Task<int> ReplayAsync(
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        Guid replayId = Guid.CreateVersion7();
        var reset = new WatchlistProjectionResetV1(
            Guid.CreateVersion7(),
            timeProvider.GetUtcNow(),
            replayId);
        await publisher.PublishAsync(
            IntegrationEventEnvelopeFactory.Create(reset, correlationId),
            cancellationToken).ConfigureAwait(false);

        OutboxDispatchItem[] history = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.EventType == WatchlistEventTypes.AddressAddedV1 ||
                message.EventType == WatchlistEventTypes.AddressUpdatedV1 ||
                message.EventType == WatchlistEventTypes.AddressRemovedV1)
            .OrderBy(message => message.OccurredAtUtc)
            .ThenBy(message => message.Id)
            .Select(message => new OutboxDispatchItem(
                message.Id,
                message.EventType,
                message.EventVersion,
                message.OccurredAtUtc,
                correlationId,
                reset.EventId,
                message.Payload,
                message.DeliveryAttempts))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        foreach (OutboxDispatchItem item in history)
        {
            await publisher.PublishAsync(
                IntegrationEventEnvelopeFactory.Create(item),
                cancellationToken).ConfigureAwait(false);
        }

        return history.Length;
    }
}
