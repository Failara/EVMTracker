namespace EVMTracker.Notification.Infrastructure.Persistence.Entities;

internal sealed class WatchlistProjectionRecord
{
    public Guid TrackedAddressId { get; set; }

    public long ChainId { get; set; }

    public long TelegramUserId { get; set; }

    public long TelegramChatId { get; set; }

    public string Address { get; set; } = string.Empty;

    public string? Label { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
