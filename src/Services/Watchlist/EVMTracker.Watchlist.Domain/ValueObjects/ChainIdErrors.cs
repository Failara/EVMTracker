using EVMTracker.Watchlist.Domain.Common;

namespace EVMTracker.Watchlist.Domain.ValueObjects;

public static class ChainIdErrors
{
    public static DomainError Invalid { get; } = new(
        "ChainId.Invalid",
        "An EVM chain ID must be a positive integer.");
}
