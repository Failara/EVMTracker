using EVMTracker.Notification.Domain.Notifications;

namespace EVMTracker.Notification.Infrastructure.Persistence.Entities;

internal sealed class TransferNotificationRecord
{
    public Guid Id { get; set; }

    public Guid SubscriptionId { get; set; }

    public Guid SourceEventId { get; set; }

    public long TelegramUserId { get; set; }

    public long TelegramChatId { get; set; }

    public long ChainId { get; set; }

    public string NetworkName { get; set; } = string.Empty;

    public string MonitoredAddress { get; set; } = string.Empty;

    public string? Label { get; set; }

    public TransferDirection Direction { get; set; }

    public string TokenContract { get; set; } = string.Empty;

    public string TokenSymbol { get; set; } = string.Empty;

    public string? TokenName { get; set; }

    public int? TokenDecimals { get; set; }

    public string RawValue { get; set; } = string.Empty;

    public string DisplayAmount { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string ToAddress { get; set; } = string.Empty;

    public string TransactionHash { get; set; } = string.Empty;

    public long BlockNumber { get; set; }

    public string BlockHash { get; set; } = string.Empty;

    public long LogIndex { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
