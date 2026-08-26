using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using EVMTracker.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence;

internal sealed class IndexerOutboxBatchStore(IndexerDbContext dbContext) : IOutboxBatchStore
{
    public async Task<IReadOnlyList<OutboxDispatchItem>> GetPendingAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.ProcessedAtUtc == null &&
                message.DeadLetteredAtUtc == null &&
                (message.NextAttemptAtUtc == null ||
                    message.NextAttemptAtUtc <= now.UtcDateTime))
            .OrderBy(message => message.Id)
            .Take(batchSize)
            .Select(message => new OutboxDispatchItem(
                message.Id,
                message.EventType,
                message.EventVersion,
                message.OccurredAtUtc,
                message.CorrelationId,
                message.CausationId,
                message.Payload,
                message.DeliveryAttempts))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task MarkPublishedAsync(
        Guid messageId,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken)
    {
        OutboxMessage message = await FindAsync(messageId, cancellationToken).ConfigureAwait(false);
        message.ProcessedAtUtc = publishedAt;
        message.DeliveryAttempts++;
        message.LastError = null;
        message.NextAttemptAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        Guid messageId,
        string failureMessage,
        DateTimeOffset nextAttemptAt,
        bool deadLetter,
        CancellationToken cancellationToken)
    {
        OutboxMessage message = await FindAsync(messageId, cancellationToken).ConfigureAwait(false);
        message.DeliveryAttempts++;
        message.LastError = failureMessage.Length <= 2000
            ? failureMessage
            : failureMessage[..2000];
        message.NextAttemptAtUtc = deadLetter ? null : nextAttemptAt.UtcDateTime;
        message.DeadLetteredAtUtc = deadLetter ? nextAttemptAt : null;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<OutboxMessage> FindAsync(Guid messageId, CancellationToken cancellationToken) =>
        await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == messageId,
            cancellationToken).ConfigureAwait(false);
}
