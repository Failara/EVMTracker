namespace EVMTracker.Notification.Application.Watchlists;

public sealed record ProjectedWatchlistEntry(
    Guid TrackedAddressId,
    long ChainId,
    long TelegramUserId,
    long TelegramChatId,
    string Address,
    string? Label,
    DateTimeOffset UpdatedAtUtc);
