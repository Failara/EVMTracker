using EVMTracker.Contracts.Watchlists;
using EVMTracker.Notification.Application.Watchlists;
using EVMTracker.Notification.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EVMTracker.Notification.Infrastructure.Persistence;

internal sealed class WatchlistProjectionStore(NotificationDbContext dbContext)
    : IWatchlistProjectionStore
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
            integrationEvent.TelegramUserId,
            integrationEvent.TelegramChatId,
            integrationEvent.Address,
            integrationEvent.Label,
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
            integrationEvent.TelegramUserId,
            integrationEvent.TelegramChatId,
            integrationEvent.Address,
            integrationEvent.Label,
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

        WatchlistProjectionRecord[] records = await dbContext.WatchlistProjection
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
        dbContext.InboxMessages.RemoveRange(
            await dbContext.InboxMessages.ToArrayAsync(cancellationToken).ConfigureAwait(false));
        AddInbox(eventId, integrationEvent.EventType, occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProjectedWatchlistEntry>> ListAsync(
        CancellationToken cancellationToken) =>
        await dbContext.WatchlistProjection
            .AsNoTracking()
            .OrderBy(record => record.TrackedAddressId)
            .ThenBy(record => record.ChainId)
            .Select(record => new ProjectedWatchlistEntry(
                record.TrackedAddressId,
                record.ChainId,
                record.TelegramUserId,
                record.TelegramChatId,
                record.Address,
                record.Label,
                record.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    private async Task ReplaceAsync(
        Guid eventId,
        string eventType,
        DateTimeOffset occurredAt,
        Guid trackedAddressId,
        long telegramUserId,
        long telegramChatId,
        string address,
        string? label,
        IReadOnlyCollection<long> chainIds,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (await IsProcessedAsync(eventId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        WatchlistProjectionRecord[] existing = await dbContext.WatchlistProjection
            .Where(record => record.TrackedAddressId == trackedAddressId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        dbContext.WatchlistProjection.RemoveRange(existing);
        foreach (long chainId in chainIds.Distinct().Order())
        {
            dbContext.WatchlistProjection.Add(
                new WatchlistProjectionRecord
                {
                    TrackedAddressId = trackedAddressId,
                    ChainId = chainId,
                    TelegramUserId = telegramUserId,
                    TelegramChatId = telegramChatId,
                    Address = address.ToLowerInvariant(),
                    Label = label,
                    UpdatedAtUtc = occurredAt,
                });
        }

        AddInbox(eventId, eventType, occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        dbContext.InboxMessages.AnyAsync(
            message => message.EventId == eventId,
            cancellationToken);

    private void AddInbox(Guid eventId, string eventType, DateTimeOffset processedAt) =>
        dbContext.InboxMessages.Add(
            new InboxMessage
            {
                EventId = eventId,
                EventType = eventType,
                ProcessedAtUtc = processedAt,
            });
}
