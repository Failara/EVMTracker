using EVMTracker.Contracts.Watchlists;

namespace EVMTracker.EvmIndexer.Application.Watchlists;

public interface IIndexerWatchlistProjectionStore
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
}
