namespace EVMTracker.Watchlist.Application.Abstractions.Persistence;

public enum WatchlistCommitResult
{
    Success = 0,
    DuplicateTrackedAddress = 1,
}
