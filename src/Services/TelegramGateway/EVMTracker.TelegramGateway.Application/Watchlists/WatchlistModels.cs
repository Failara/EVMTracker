namespace EVMTracker.TelegramGateway.Application.Watchlists;

public sealed record AddTrackedAddressRequest(
    string Address,
    long TelegramUserId,
    long TelegramChatId,
    long ChainId,
    string? Label);

public sealed record RemoveTrackedAddressNetworkRequest(
    string Address,
    long TelegramUserId,
    long ChainId);

public sealed record TrackedAddressView(
    Guid Id,
    string Address,
    long TelegramUserId,
    long TelegramChatId,
    string? Label,
    IReadOnlyCollection<long> ChainIds,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record WatchlistServiceStatus(bool IsAvailable);
