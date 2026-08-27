using System.Text.Json;
using EVMTracker.Contracts.Notifications;
using EVMTracker.Contracts.Transfers;
using EVMTracker.Notification.Application.Transfers;
using EVMTracker.Notification.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EVMTracker.Notification.Infrastructure.Persistence;

internal sealed class TransferOrphanStore(
    NotificationDbContext dbContext,
    INetworkPresentationResolver networkResolver) : ITransferOrphanStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<OrphanProcessingResult> ApplyAsync(
        TransferIntegrationEventEnvelope envelope,
        Erc20TransferOrphanedV1 orphanedTransfer,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (await dbContext.InboxMessages.AnyAsync(
                message => message.EventId == envelope.EventId,
                cancellationToken).ConfigureAwait(false))
        {
            return new OrphanProcessingResult(0, 0, 0);
        }

        bool logicalOrphanExists = await dbContext.OrphanedTransfers.AnyAsync(
            orphan => orphan.ChainId == orphanedTransfer.ChainId &&
                orphan.TransactionHash == orphanedTransfer.TransactionHash &&
                orphan.LogIndex == orphanedTransfer.LogIndex &&
                orphan.FormerBlockHash == orphanedTransfer.FormerBlockHash,
            cancellationToken).ConfigureAwait(false);
        if (!logicalOrphanExists)
        {
            dbContext.OrphanedTransfers.Add(
                new OrphanedTransferRecord
                {
                    EventId = envelope.EventId,
                    ChainId = orphanedTransfer.ChainId,
                    TransactionHash = orphanedTransfer.TransactionHash,
                    LogIndex = orphanedTransfer.LogIndex,
                    FormerBlockHash = orphanedTransfer.FormerBlockHash,
                    OrphanedAtUtc = envelope.OccurredAt,
                });
        }

        TransferNotificationRecord[] notifications = await dbContext.TransferNotifications
            .Where(notification =>
                notification.ChainId == orphanedTransfer.ChainId &&
                notification.TransactionHash == orphanedTransfer.TransactionHash &&
                notification.LogIndex == orphanedTransfer.LogIndex &&
                notification.BlockHash == orphanedTransfer.FormerBlockHash &&
                !notification.IsOrphaned)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] deliveryEventIds = notifications
            .Select(notification => notification.DeliveryEventId)
            .ToArray();
        Dictionary<Guid, OutboxMessage> originalDeliveries = deliveryEventIds.Length == 0
            ? []
            : await dbContext.OutboxMessages
                .Where(message => deliveryEventIds.Contains(message.Id))
                .ToDictionaryAsync(message => message.Id, cancellationToken)
                .ConfigureAwait(false);

        int corrections = 0;
        int cancelled = 0;
        NetworkPresentation network = networkResolver.Resolve(orphanedTransfer.ChainId);
        foreach (TransferNotificationRecord notification in notifications)
        {
            notification.IsOrphaned = true;
            notification.OrphanedAtUtc = envelope.OccurredAt;
            if (!originalDeliveries.TryGetValue(
                    notification.DeliveryEventId,
                    out OutboxMessage? original) ||
                original.ProcessedAtUtc is null)
            {
                if (original is not null && original.DeadLetteredAtUtc is null)
                {
                    original.DeadLetteredAtUtc = envelope.OccurredAt;
                    original.LastError =
                        "Cancelled because the transfer became orphaned before delivery publication.";
                    original.NextAttemptAtUtc = null;
                    cancelled++;
                }

                continue;
            }

            Guid correctionId = NotificationDeliveryIdentity.CreateCorrectionDeliveryId(
                notification.Id,
                envelope.EventId);
            notification.CorrectionDeliveryEventId = correctionId;
            if (await dbContext.OutboxMessages.AnyAsync(
                    message => message.Id == correctionId,
                    cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            string message = TransferOrphanCorrectionRenderer.Render(
                new OrphanedTransferNotification(
                    network.DisplayName,
                    network.ExplorerBaseUrl,
                    notification.MonitoredAddress,
                    notification.Label,
                    notification.TransactionHash,
                    orphanedTransfer.FormerBlockHash,
                    notification.BlockNumber));
            var correction = new TelegramDeliveryRequestedV1(
                correctionId,
                envelope.OccurredAt,
                correctionId,
                notification.TelegramChatId,
                message,
                "HTML");
            dbContext.OutboxMessages.Add(
                new OutboxMessage
                {
                    Id = correction.EventId,
                    EventType = correction.EventType,
                    EventVersion = correction.EventVersion,
                    OccurredAtUtc = correction.OccurredAtUtc,
                    CorrelationId = envelope.CorrelationId,
                    CausationId = envelope.EventId,
                    Payload = JsonSerializer.Serialize(correction, SerializerOptions),
                });
            corrections++;
        }

        dbContext.InboxMessages.Add(
            new InboxMessage
            {
                EventId = envelope.EventId,
                EventType = envelope.EventType,
                ProcessedAtUtc = envelope.OccurredAt,
            });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new OrphanProcessingResult(notifications.Length, corrections, cancelled);
    }
}
