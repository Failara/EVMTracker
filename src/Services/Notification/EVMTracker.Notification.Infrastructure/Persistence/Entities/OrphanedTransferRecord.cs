namespace EVMTracker.Notification.Infrastructure.Persistence.Entities;

internal sealed class OrphanedTransferRecord
{
    public Guid EventId { get; set; }

    public long ChainId { get; set; }

    public string TransactionHash { get; set; } = string.Empty;

    public long LogIndex { get; set; }

    public string FormerBlockHash { get; set; } = string.Empty;

    public DateTimeOffset OrphanedAtUtc { get; set; }
}
