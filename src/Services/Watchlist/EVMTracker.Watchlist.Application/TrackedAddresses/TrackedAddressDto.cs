using EVMTracker.Watchlist.Domain.TrackedAddresses;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

public sealed record TrackedAddressDto(
    Guid Id,
    string Address,
    long TelegramUserId,
    long TelegramChatId,
    string? Label,
    IReadOnlyCollection<long> ChainIds,
    bool IsActive,
    DateTimeOffset CreatedAtUtc)
{
    public static TrackedAddressDto FromDomain(TrackedAddress trackedAddress)
    {
        ArgumentNullException.ThrowIfNull(trackedAddress);

        return new TrackedAddressDto(
            trackedAddress.Id,
            trackedAddress.Address.Value,
            trackedAddress.TelegramUserId,
            trackedAddress.TelegramChatId,
            trackedAddress.Label,
            trackedAddress.EnabledNetworks.Select(chainId => chainId.Value).ToArray(),
            trackedAddress.IsActive,
            trackedAddress.CreatedAtUtc);
    }
}
