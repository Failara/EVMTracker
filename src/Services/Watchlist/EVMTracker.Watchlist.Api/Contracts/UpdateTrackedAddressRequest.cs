namespace EVMTracker.Watchlist.Api.Contracts;

public sealed record UpdateTrackedAddressRequest(
    long TelegramUserId,
    IReadOnlyCollection<long> ChainIds,
    string? Label);
