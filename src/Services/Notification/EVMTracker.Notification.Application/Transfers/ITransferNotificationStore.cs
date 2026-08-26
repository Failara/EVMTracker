using EVMTracker.Contracts.Transfers;

namespace EVMTracker.Notification.Application.Transfers;

public interface ITransferNotificationStore
{
    Task<int> PersistAsync(
        TransferIntegrationEventEnvelope envelope,
        IReadOnlyCollection<PreparedTransferNotification> notifications,
        CancellationToken cancellationToken);
}
