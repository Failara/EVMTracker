namespace EVMTracker.Watchlist.Application.Abstractions.Messaging;

public interface IWatchlistEventReplayService
{
    Task<int> ReplayAsync(Guid correlationId, CancellationToken cancellationToken);
}
