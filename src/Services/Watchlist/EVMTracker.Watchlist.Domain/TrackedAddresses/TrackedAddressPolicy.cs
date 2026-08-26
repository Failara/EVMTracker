using EVMTracker.Watchlist.Domain.Common;
using EVMTracker.Watchlist.Domain.ValueObjects;

namespace EVMTracker.Watchlist.Domain.TrackedAddresses;

public static class TrackedAddressPolicy
{
    public static Result EnsureCanTrack(
        long telegramUserId,
        Address address,
        ChainId chainId,
        IEnumerable<TrackedAddress> existingTrackedAddresses)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(chainId);
        ArgumentNullException.ThrowIfNull(existingTrackedAddresses);

        bool alreadyTracked = existingTrackedAddresses.Any(existing =>
            existing.IsActive &&
            existing.TelegramUserId == telegramUserId &&
            existing.Address == address &&
            existing.IsEnabledOn(chainId));

        return alreadyTracked
            ? Result.Failure(TrackedAddressErrors.AlreadyTracked)
            : Result.Success();
    }
}
