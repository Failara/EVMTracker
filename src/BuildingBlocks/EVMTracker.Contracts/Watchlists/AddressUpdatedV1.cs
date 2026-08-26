namespace EVMTracker.Contracts.Watchlists;

public sealed record AddressUpdatedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid TrackedAddressId,
    long TelegramUserId,
    long TelegramChatId,
    string Address,
    string? Label,
    IReadOnlyCollection<long> ChainIds) : IWatchlistIntegrationEvent
{
    public string EventType => WatchlistEventTypes.AddressUpdatedV1;
}
