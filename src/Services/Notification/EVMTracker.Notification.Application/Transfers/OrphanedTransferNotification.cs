namespace EVMTracker.Notification.Application.Transfers;

public sealed record OrphanedTransferNotification(
    string NetworkName,
    string? ExplorerBaseUrl,
    string MonitoredAddress,
    string? Label,
    string TransactionHash,
    string FormerBlockHash,
    long BlockNumber);
