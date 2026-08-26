namespace EVMTracker.Contracts.Watchlists;

public interface IWatchlistIntegrationEvent
{
    Guid EventId { get; }

    string EventType { get; }

    DateTimeOffset OccurredAtUtc { get; }
}
