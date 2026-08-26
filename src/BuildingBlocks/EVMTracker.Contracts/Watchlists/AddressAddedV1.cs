namespace EVMTracker.Contracts.Watchlists;

public sealed record AddressAddedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid TrackedAddressId,
    long TelegramUserId,
    long TelegramChatId,
    string Address,
    string? Label,
    IReadOnlyCollection<long> ChainIds) : IWatchlistIntegrationEvent
{
    public string EventType => WatchlistEventTypes.AddressAddedV1;

    public int EventVersion => 1;
}
