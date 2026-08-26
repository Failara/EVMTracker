namespace EVMTracker.Contracts.Watchlists;

public sealed record WatchlistProjectionResetV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid ReplayId) : IWatchlistIntegrationEvent
{
    public string EventType => WatchlistEventTypes.ProjectionResetV1;

    public int EventVersion => 1;
}
