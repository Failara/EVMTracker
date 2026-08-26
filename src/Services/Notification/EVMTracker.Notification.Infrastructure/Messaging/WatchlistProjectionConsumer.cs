using EVMTracker.Contracts.Watchlists;
using EVMTracker.Notification.Application.Watchlists;
using MassTransit;

namespace EVMTracker.Notification.Infrastructure.Messaging;

internal sealed class WatchlistProjectionConsumer(WatchlistProjectionProjector projector)
    : IConsumer<WatchlistIntegrationEventEnvelope>
{
    public Task Consume(ConsumeContext<WatchlistIntegrationEventEnvelope> context) =>
        projector.ProjectAsync(context.Message, context.CancellationToken);
}
