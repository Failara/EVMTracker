using EVMTracker.Watchlist.Domain.Common;
using EVMTracker.Watchlist.Domain.TrackedAddresses;
using EVMTracker.Watchlist.Domain.ValueObjects;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

internal static class ChainIdCollection
{
    public static Result<IReadOnlyCollection<ChainId>> Create(
        IReadOnlyCollection<long>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Result.Failure<IReadOnlyCollection<ChainId>>(
                TrackedAddressErrors.NetworkRequired);
        }

        var chainIds = new List<ChainId>(values.Count);
        var uniqueValues = new HashSet<long>();

        foreach (long value in values)
        {
            Result<ChainId> chainIdResult = ChainId.Create(value);
            if (chainIdResult.IsFailure)
            {
                return Result.Failure<IReadOnlyCollection<ChainId>>(chainIdResult.Error);
            }

            if (!uniqueValues.Add(value))
            {
                return Result.Failure<IReadOnlyCollection<ChainId>>(
                    TrackedAddressErrors.DuplicateNetwork);
            }

            chainIds.Add(chainIdResult.Value);
        }

        return Result.Success<IReadOnlyCollection<ChainId>>(chainIds);
    }
}
