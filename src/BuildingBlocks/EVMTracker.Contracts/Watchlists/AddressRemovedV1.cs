namespace EVMTracker.Contracts.Watchlists;

public sealed record AddressRemovedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid TrackedAddressId,
    long TelegramUserId,
    long TelegramChatId,
    string Address) : IWatchlistIntegrationEvent
{
    public string EventType => WatchlistEventTypes.AddressRemovedV1;
}
