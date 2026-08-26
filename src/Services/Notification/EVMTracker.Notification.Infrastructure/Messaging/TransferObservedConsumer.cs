using EVMTracker.Contracts.Transfers;
using EVMTracker.Notification.Application.Transfers;
using MassTransit;

namespace EVMTracker.Notification.Infrastructure.Messaging;

internal sealed class TransferObservedConsumer(TransferEventProcessor processor)
    : IConsumer<TransferIntegrationEventEnvelope>
{
    public async Task Consume(ConsumeContext<TransferIntegrationEventEnvelope> context)
    {
        await processor.ProcessAsync(
            context.Message,
            context.CancellationToken).ConfigureAwait(false);
    }
}
