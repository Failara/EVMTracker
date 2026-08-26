namespace EVMTracker.Watchlist.Infrastructure.Persistence.Entities;

internal sealed class TrackedAddressRecord
{
    public Guid Id { get; set; }

    public string Address { get; set; } = string.Empty;

    public long TelegramUserId { get; set; }

    public long TelegramChatId { get; set; }

    public string? Label { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<TrackedAddressNetworkRecord> Networks { get; } = [];
}
