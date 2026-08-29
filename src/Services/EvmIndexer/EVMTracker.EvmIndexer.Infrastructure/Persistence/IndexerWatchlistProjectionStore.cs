using EVMTracker.Contracts.Watchlists;
using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Application.Watchlists;
using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence;

internal sealed class IndexerWatchlistProjectionStore(IndexerDbContext dbContext)
    : IIndexerWatchlistProjectionStore, ITrackedAddressFilter
{
    public Task ApplyAddedAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        AddressAddedV1 integrationEvent,
        CancellationToken cancellationToken) =>
        ReplaceAsync(
            eventId,
            integrationEvent.EventType,
            occurredAt,
            integrationEvent.TrackedAddressId,
            integrationEvent.Address,
            integrationEvent.ChainIds,
            cancellationToken);

    public Task ApplyUpdatedAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        AddressUpdatedV1 integrationEvent,
        CancellationToken cancellationToken) =>
        ReplaceAsync(
            eventId,
            integrationEvent.EventType,
            occurredAt,
            integrationEvent.TrackedAddressId,
            integrationEvent.Address,
            integrationEvent.ChainIds,
            cancellationToken);

    public async Task ApplyRemovedAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        AddressRemovedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (await IsProcessedAsync(eventId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        IndexerWatchlistProjectionRecord[] records = await dbContext.WatchlistProjection
            .Where(record => record.TrackedAddressId == integrationEvent.TrackedAddressId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        dbContext.WatchlistProjection.RemoveRange(records);
        AddInbox(eventId, integrationEvent.EventType, occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        WatchlistProjectionResetV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (await IsProcessedAsync(eventId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        dbContext.WatchlistProjection.RemoveRange(
            await dbContext.WatchlistProjection.ToArrayAsync(cancellationToken)
                .ConfigureAwait(false));
        dbContext.WatchlistInboxMessages.RemoveRange(
            await dbContext.WatchlistInboxMessages.ToArrayAsync(cancellationToken)
                .ConfigureAwait(false));
        AddInbox(eventId, integrationEvent.EventType, occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<string>> ListAsync(
        long chainId,
        CancellationToken cancellationToken) =>
        await dbContext.WatchlistProjection
            .AsNoTracking()
            .Where(record => record.ChainId == chainId)
            .Select(record => record.Address)
            .Distinct()
            .Order()
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    private async Task ReplaceAsync(
        Guid eventId,
        string eventType,
        DateTimeOffset occurredAt,
        Guid trackedAddressId,
        string address,
        IReadOnlyCollection<long> chainIds,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (await IsProcessedAsync(eventId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        IndexerWatchlistProjectionRecord[] existing = await dbContext.WatchlistProjection
            .Where(record => record.TrackedAddressId == trackedAddressId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        dbContext.WatchlistProjection.RemoveRange(existing);
        foreach (long chainId in chainIds.Distinct().Order())
        {
            dbContext.WatchlistProjection.Add(
                new IndexerWatchlistProjectionRecord
                {
                    TrackedAddressId = trackedAddressId,
                    ChainId = chainId,
                    Address = address.ToLowerInvariant(),
                    UpdatedAtUtc = occurredAt,
                });
        }

        AddInbox(eventId, eventType, occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        dbContext.WatchlistInboxMessages.AnyAsync(
            message => message.EventId == eventId,
            cancellationToken);

    private void AddInbox(Guid eventId, string eventType, DateTimeOffset processedAt) =>
        dbContext.WatchlistInboxMessages.Add(
            new IndexerWatchlistInboxRecord
            {
                EventId = eventId,
                EventType = eventType,
                ProcessedAtUtc = processedAt,
            });
}
