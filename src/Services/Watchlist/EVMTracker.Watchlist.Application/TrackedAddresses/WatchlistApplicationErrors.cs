using EVMTracker.Watchlist.Domain.Common;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

public static class WatchlistApplicationErrors
{
    public static DomainError NotFound { get; } = new(
        "TrackedAddress.NotFound",
        "The tracked address does not exist.");
}
