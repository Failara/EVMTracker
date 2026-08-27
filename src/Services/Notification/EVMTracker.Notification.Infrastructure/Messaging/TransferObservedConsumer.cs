using System.Diagnostics;
using EVMTracker.Contracts.Transfers;
using EVMTracker.Notification.Application.Transfers;
using EVMTracker.Observability;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EVMTracker.Notification.Infrastructure.Messaging;

internal sealed class TransferObservedConsumer(
    TransferEventProcessor observedProcessor,
    TransferOrphanProcessor orphanProcessor,
    ILogger<TransferObservedConsumer> logger)
    : IConsumer<TransferIntegrationEventEnvelope>
{
    public async Task Consume(ConsumeContext<TransferIntegrationEventEnvelope> context)
    {
        TransferIntegrationEventEnvelope envelope = context.Message;
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
        if (context.Message.EventType == TransferEventTypes.Erc20TransferObservedV1)
        {
            TransferProcessingResult result = await observedProcessor.ProcessAsync(
                context.Message,
                context.CancellationToken).ConfigureAwait(false);
            EVMTrackerTelemetry.RecordNotificationMatch(
                result.MatchedSubscriptions,
                result.CreatedNotifications);
            activity?.SetTag("notification.matches", result.MatchedSubscriptions);
            activity?.SetTag("notification.created", result.CreatedNotifications);
            return;
        }

        OrphanProcessingResult orphanResult = await orphanProcessor.ProcessAsync(
            context.Message,
            context.CancellationToken).ConfigureAwait(false);
        activity?.SetTag("notification.orphaned", orphanResult.OrphanedNotifications);
        activity?.SetTag("notification.corrections", orphanResult.CorrectionsCreated);
    }
}
