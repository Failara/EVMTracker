using EVMTracker.Watchlist.Application.Abstractions.Persistence;
using EVMTracker.Watchlist.Domain.TrackedAddresses;
using EVMTracker.Watchlist.Domain.ValueObjects;
using EVMTracker.Watchlist.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.Watchlist.Infrastructure.Persistence;

internal sealed class TrackedAddressRepository(WatchlistDbContext dbContext)
    : ITrackedAddressRepository
{
    public void Add(TrackedAddress trackedAddress)
    {
        ArgumentNullException.ThrowIfNull(trackedAddress);

        dbContext.TrackedAddresses.Add(ToRecord(trackedAddress));
    }

    public async Task<TrackedAddress?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        TrackedAddressRecord? record = await dbContext.TrackedAddresses
            .AsNoTracking()
            .Include(item => item.Networks)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<TrackedAddress>> ListByUserAsync(
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        TrackedAddressRecord[] records = await dbContext.TrackedAddresses
            .AsNoTracking()
            .Include(item => item.Networks)
            .Where(item => item.TelegramUserId == telegramUserId && item.IsActive)
            .OrderBy(item => item.CreatedAtUtc)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<TrackedAddress>> ListByUserAndAddressAsync(
        long telegramUserId,
        Address address,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        TrackedAddressRecord[] records = await dbContext.TrackedAddresses
            .AsNoTracking()
            .Include(item => item.Networks)
            .Where(item =>
                item.TelegramUserId == telegramUserId &&
                item.Address == address.Value)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.Select(ToDomain).ToArray();
    }

    public async Task UpdateAsync(
        TrackedAddress trackedAddress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trackedAddress);

        TrackedAddressRecord record = await dbContext.TrackedAddresses
            .Include(item => item.Networks)
            .SingleAsync(item => item.Id == trackedAddress.Id, cancellationToken)
            .ConfigureAwait(false);

        record.TelegramChatId = trackedAddress.TelegramChatId;
        record.Label = trackedAddress.Label;
        record.IsActive = trackedAddress.IsActive;

        HashSet<long> desiredChainIds = trackedAddress.IsActive
            ? trackedAddress.EnabledNetworks
                .Select(chainId => chainId.Value)
                .ToHashSet()
            : [];
        TrackedAddressNetworkRecord[] removedNetworks = record.Networks
            .Where(network => !desiredChainIds.Contains(network.ChainId))
            .ToArray();

        dbContext.TrackedAddressNetworks.RemoveRange(removedNetworks);

        HashSet<long> currentChainIds = record.Networks
            .Select(network => network.ChainId)
            .ToHashSet();
        foreach (long chainId in desiredChainIds.Except(currentChainIds))
        {
            record.Networks.Add(CreateNetworkRecord(trackedAddress, chainId));
        }
    }

    private static TrackedAddressRecord ToRecord(TrackedAddress trackedAddress)
    {
        var record = new TrackedAddressRecord
        {
            Id = trackedAddress.Id,
            Address = trackedAddress.Address.Value,
            TelegramUserId = trackedAddress.TelegramUserId,
            TelegramChatId = trackedAddress.TelegramChatId,
            Label = trackedAddress.Label,
            IsActive = trackedAddress.IsActive,
            CreatedAtUtc = trackedAddress.CreatedAtUtc,
        };

        foreach (long chainId in trackedAddress.EnabledNetworks.Select(item => item.Value))
        {
            record.Networks.Add(CreateNetworkRecord(trackedAddress, chainId));
        }

        return record;
    }

    private static TrackedAddressNetworkRecord CreateNetworkRecord(
        TrackedAddress trackedAddress,
        long chainId) =>
        new()
        {
            TrackedAddressId = trackedAddress.Id,
            ChainId = chainId,
            TelegramUserId = trackedAddress.TelegramUserId,
            Address = trackedAddress.Address.Value,
        };

    private static TrackedAddress ToDomain(TrackedAddressRecord record)
    {
        Address address = Address.Create(record.Address).Value;
        ChainId[] chainIds = record.Networks
            .Select(network => ChainId.Create(network.ChainId).Value)
            .ToArray();

        return TrackedAddress.Rehydrate(
            record.Id,
            address,
            record.TelegramUserId,
            record.TelegramChatId,
            record.Label,
            chainIds,
            record.CreatedAtUtc,
            record.IsActive);
    }
}
