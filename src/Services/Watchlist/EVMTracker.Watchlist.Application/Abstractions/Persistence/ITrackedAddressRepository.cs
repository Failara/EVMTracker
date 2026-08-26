using EVMTracker.Watchlist.Domain.TrackedAddresses;
using EVMTracker.Watchlist.Domain.ValueObjects;

namespace EVMTracker.Watchlist.Application.Abstractions.Persistence;

public interface ITrackedAddressRepository
{
    void Add(TrackedAddress trackedAddress);

    Task<TrackedAddress?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrackedAddress>> ListByUserAsync(
        long telegramUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TrackedAddress>> ListByUserAndAddressAsync(
        long telegramUserId,
        Address address,
        CancellationToken cancellationToken);

    Task UpdateAsync(TrackedAddress trackedAddress, CancellationToken cancellationToken);
}
