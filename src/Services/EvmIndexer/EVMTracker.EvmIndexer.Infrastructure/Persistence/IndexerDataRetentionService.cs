using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence;

internal sealed record IndexerRetentionResult(
    int OutboxMessages,
    int ProcessedTransfers,
    int WatchlistInboxMessages);

internal sealed class IndexerDataRetentionService(
    IndexerDbContext dbContext,
    IOptions<IndexerDataRetentionOptions> options,
    TimeProvider timeProvider)
{
    public async Task<IndexerRetentionResult> PruneAsync(
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset outboxCutoff = now - options.Value.PublishedOutboxRetention;
        DateTimeOffset transferCutoff = now - options.Value.TransferHistoryRetention;
        DateTimeOffset watchlistCutoff = now - options.Value.WatchlistInboxRetention;

        var completedOutbox = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.ProcessedAtUtc != null || message.DeadLetteredAtUtc != null)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var expiredOutbox = completedOutbox
            .Where(message => message.OccurredAtUtc < outboxCutoff)
            .Take(options.Value.BatchSize)
            .ToArray();
        dbContext.OutboxMessages.RemoveRange(expiredOutbox);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Guid[] pendingEventIds = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.ProcessedAtUtc == null && message.DeadLetteredAtUtc == null)
            .Select(message => message.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        HashSet<Guid> pendingEvents = pendingEventIds.ToHashSet();
        var transferHistory = await dbContext.ProcessedTransfers
            .AsNoTracking()
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var expiredTransfers = transferHistory
            .Where(transfer =>
                transfer.ObservedAtUtc < transferCutoff &&
                !pendingEvents.Contains(transfer.ObservedEventId))
            .Take(options.Value.BatchSize)
            .ToArray();
        dbContext.ProcessedTransfers.RemoveRange(expiredTransfers);

        var watchlistInbox = await dbContext.WatchlistInboxMessages
            .AsNoTracking()
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var expiredWatchlistInbox = watchlistInbox
            .Where(message => message.ProcessedAtUtc < watchlistCutoff)
            .Take(options.Value.BatchSize)
            .ToArray();
        dbContext.WatchlistInboxMessages.RemoveRange(expiredWatchlistInbox);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IndexerRetentionResult(
            expiredOutbox.Length,
            expiredTransfers.Length,
            expiredWatchlistInbox.Length);
    }
}
