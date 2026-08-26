using EVMTracker.Contracts.Watchlists;

namespace EVMTracker.Notification.Application.Watchlists;

public interface IWatchlistProjectionStore
{
    Task ApplyAddedAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        AddressAddedV1 integrationEvent,
        CancellationToken cancellationToken);

    Task ApplyUpdatedAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        AddressUpdatedV1 integrationEvent,
        CancellationToken cancellationToken);

    Task ApplyRemovedAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        AddressRemovedV1 integrationEvent,
        CancellationToken cancellationToken);

    Task ResetAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        WatchlistProjectionResetV1 integrationEvent,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectedWatchlistEntry>> ListAsync(
        CancellationToken cancellationToken);
}
