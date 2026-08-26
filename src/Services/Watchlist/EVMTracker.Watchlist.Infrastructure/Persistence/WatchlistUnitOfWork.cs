using EVMTracker.Watchlist.Application.Abstractions.Persistence;
using EVMTracker.Watchlist.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EVMTracker.Watchlist.Infrastructure.Persistence;

internal sealed class WatchlistUnitOfWork(WatchlistDbContext dbContext)
    : IWatchlistUnitOfWork
{
    public async Task<WatchlistCommitResult> SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WatchlistCommitResult.Success;
        }
        catch (DbUpdateException exception) when (IsUniqueTrackingViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            return WatchlistCommitResult.DuplicateTrackedAddress;
        }
    }

    private static bool IsUniqueTrackingViolation(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException postgresException)
        {
            return string.Equals(
                postgresException.SqlState,
                PostgresErrorCodes.UniqueViolation,
                StringComparison.Ordinal) &&
                string.Equals(
                    postgresException.ConstraintName,
                    TrackedAddressNetworkRecordConfiguration.UniqueTrackingConstraintName,
                    StringComparison.Ordinal);
        }

        return exception.InnerException?.Message.Contains(
            "UNIQUE constraint failed",
            StringComparison.OrdinalIgnoreCase) == true;
    }
}
