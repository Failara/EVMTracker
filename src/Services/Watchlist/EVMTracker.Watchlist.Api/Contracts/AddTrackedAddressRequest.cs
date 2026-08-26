namespace EVMTracker.Watchlist.Api.Contracts;

public sealed record AddTrackedAddressRequest(
    string Address,
    long TelegramUserId,
    long TelegramChatId,
    IReadOnlyCollection<long> ChainIds,
    string? Label);
