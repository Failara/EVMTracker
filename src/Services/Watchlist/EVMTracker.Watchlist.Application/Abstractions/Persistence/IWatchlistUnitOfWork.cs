namespace EVMTracker.Watchlist.Application.Abstractions.Persistence;

public interface IWatchlistUnitOfWork
{
    Task<WatchlistCommitResult> SaveChangesAsync(CancellationToken cancellationToken);
}
