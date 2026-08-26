namespace EVMTracker.Watchlist.Infrastructure.Persistence.Entities;

internal sealed class TrackedAddressNetworkRecord
{
    public Guid TrackedAddressId { get; set; }

    public long ChainId { get; set; }

    public long TelegramUserId { get; set; }

    public string Address { get; set; } = string.Empty;

    public TrackedAddressRecord TrackedAddress { get; set; } = null!;
}
