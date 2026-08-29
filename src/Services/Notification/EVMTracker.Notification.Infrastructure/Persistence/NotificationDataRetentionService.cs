using EVMTracker.Notification.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EVMTracker.Notification.Infrastructure.Persistence;

internal sealed record NotificationRetentionResult(
    int OutboxMessages,
    int InboxMessages,
    int TransferNotifications,
    int OrphanedTransfers);

internal sealed class NotificationDataRetentionService(
    NotificationDbContext dbContext,
    IOptions<NotificationDataRetentionOptions> options,
    TimeProvider timeProvider)
{
    public async Task<NotificationRetentionResult> PruneAsync(
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset outboxCutoff = now - options.Value.PublishedOutboxRetention;
        DateTimeOffset inboxCutoff = now - options.Value.InboxRetention;
        DateTimeOffset historyCutoff = now - options.Value.NotificationHistoryRetention;

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

        Guid[] pendingOutboxIds = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.ProcessedAtUtc == null && message.DeadLetteredAtUtc == null)
            .Select(message => message.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        HashSet<Guid> pendingOutbox = pendingOutboxIds.ToHashSet();
        var notifications = await dbContext.TransferNotifications
            .AsNoTracking()
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var expiredNotifications = notifications
            .Where(notification =>
                notification.CreatedAtUtc < historyCutoff &&
                !pendingOutbox.Contains(notification.DeliveryEventId) &&
                (notification.CorrectionDeliveryEventId is null ||
                    !pendingOutbox.Contains(notification.CorrectionDeliveryEventId.Value)))
            .Take(options.Value.BatchSize)
            .ToArray();
        dbContext.TransferNotifications.RemoveRange(expiredNotifications);

        var orphanHistory = await dbContext.OrphanedTransfers
            .AsNoTracking()
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var expiredOrphans = orphanHistory
            .Where(orphan => orphan.OrphanedAtUtc < historyCutoff)
            .Take(options.Value.BatchSize)
            .ToArray();
        dbContext.OrphanedTransfers.RemoveRange(expiredOrphans);

        var inbox = await dbContext.InboxMessages
            .AsNoTracking()
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var expiredInbox = inbox
            .Where(message => message.ProcessedAtUtc < inboxCutoff)
            .Take(options.Value.BatchSize)
            .ToArray();
        dbContext.InboxMessages.RemoveRange(expiredInbox);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new NotificationRetentionResult(
            expiredOutbox.Length,
            expiredInbox.Length,
            expiredNotifications.Length,
            expiredOrphans.Length);
    }
}
