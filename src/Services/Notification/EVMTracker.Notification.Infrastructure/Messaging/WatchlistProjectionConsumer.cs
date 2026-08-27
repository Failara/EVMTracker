using System.Diagnostics;
using EVMTracker.Contracts.Watchlists;
using EVMTracker.Notification.Application.Watchlists;
using EVMTracker.Observability;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EVMTracker.Notification.Infrastructure.Messaging;

internal sealed class WatchlistProjectionConsumer(
    WatchlistProjectionProjector projector,
    ILogger<WatchlistProjectionConsumer> logger)
    : IConsumer<WatchlistIntegrationEventEnvelope>
{
    public async Task Consume(ConsumeContext<WatchlistIntegrationEventEnvelope> context)
    {
        WatchlistIntegrationEventEnvelope envelope = context.Message;
        using Activity? activity = EVMTrackerTelemetry.StartIntegrationEventActivity(
            "consume",
            envelope.EventType,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.CausationId,
            ActivityKind.Consumer);
        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = envelope.CorrelationId,
                ["EventId"] = envelope.EventId,
            });
        await projector.ProjectAsync(
            envelope,
            context.CancellationToken).ConfigureAwait(false);
    }
}
