using System.Diagnostics;
using EVMTracker.Contracts.Watchlists;
using EVMTracker.EvmIndexer.Application.Watchlists;
using EVMTracker.Observability;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EVMTracker.EvmIndexer.Infrastructure.Messaging;

internal sealed class IndexerWatchlistProjectionConsumer(
    IndexerWatchlistProjectionProjector projector,
    ILogger<IndexerWatchlistProjectionConsumer> logger)
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
