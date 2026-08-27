using EVMTracker.Contracts.Transfers;
using EVMTracker.Notification.Application.Transfers;
using MassTransit;

namespace EVMTracker.Notification.Infrastructure.Messaging;

internal sealed class TransferObservedConsumer(
    TransferEventProcessor observedProcessor,
    TransferOrphanProcessor orphanProcessor)
    : IConsumer<TransferIntegrationEventEnvelope>
{
    public async Task Consume(ConsumeContext<TransferIntegrationEventEnvelope> context)
    {
        if (context.Message.EventType == TransferEventTypes.Erc20TransferObservedV1)
        {
            await observedProcessor.ProcessAsync(
                context.Message,
                context.CancellationToken).ConfigureAwait(false);
            return;
        }

        await orphanProcessor.ProcessAsync(
            context.Message,
            context.CancellationToken).ConfigureAwait(false);
    }
}
