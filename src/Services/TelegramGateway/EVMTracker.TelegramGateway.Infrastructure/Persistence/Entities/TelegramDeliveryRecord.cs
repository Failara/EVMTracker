using EVMTracker.TelegramGateway.Application.Delivery;

namespace EVMTracker.TelegramGateway.Infrastructure.Persistence.Entities;

internal sealed class TelegramDeliveryRecord
{
    public Guid EventId { get; set; }

    public Guid NotificationId { get; set; }

    public long TelegramChatId { get; set; }

    public string Message { get; set; } = string.Empty;

    public string ParseMode { get; set; } = string.Empty;

    public TelegramDeliveryStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public int? TelegramMessageId { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? DeliveredAtUtc { get; set; }
}
