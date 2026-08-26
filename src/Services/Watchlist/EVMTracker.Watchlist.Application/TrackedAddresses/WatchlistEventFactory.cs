using EVMTracker.Contracts.Watchlists;
using EVMTracker.Watchlist.Domain.TrackedAddresses;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

internal static class WatchlistEventFactory
{
    public static AddressAddedV1 CreateAdded(
        TrackedAddress trackedAddress,
        DateTimeOffset occurredAtUtc) =>
        new(
            Guid.CreateVersion7(),
            occurredAtUtc,
            trackedAddress.Id,
            trackedAddress.TelegramUserId,
            trackedAddress.TelegramChatId,
            trackedAddress.Address.Value,
            trackedAddress.Label,
            GetChainIds(trackedAddress));

    public static AddressUpdatedV1 CreateUpdated(
        TrackedAddress trackedAddress,
        DateTimeOffset occurredAtUtc) =>
        new(
            Guid.CreateVersion7(),
            occurredAtUtc,
            trackedAddress.Id,
            trackedAddress.TelegramUserId,
            trackedAddress.TelegramChatId,
            trackedAddress.Address.Value,
            trackedAddress.Label,
            GetChainIds(trackedAddress));

    public static AddressRemovedV1 CreateRemoved(
        TrackedAddress trackedAddress,
        DateTimeOffset occurredAtUtc) =>
        new(
            Guid.CreateVersion7(),
            occurredAtUtc,
            trackedAddress.Id,
            trackedAddress.TelegramUserId,
            trackedAddress.TelegramChatId,
            trackedAddress.Address.Value);

    private static long[] GetChainIds(TrackedAddress trackedAddress) =>
        trackedAddress.EnabledNetworks.Select(chainId => chainId.Value).ToArray();
}
