using EVMTracker.Contracts.Transfers;

namespace EVMTracker.Notification.Application.Transfers;

public interface ITransferOrphanStore
{
    Task<OrphanProcessingResult> ApplyAsync(
        TransferIntegrationEventEnvelope envelope,
        Erc20TransferOrphanedV1 orphanedTransfer,
        CancellationToken cancellationToken);
}
