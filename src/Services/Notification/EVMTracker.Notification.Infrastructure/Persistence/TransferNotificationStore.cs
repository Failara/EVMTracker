using System.Text.Json;
using EVMTracker.Contracts.Transfers;
using EVMTracker.Notification.Application.Transfers;
using EVMTracker.Notification.Domain.Notifications;
using EVMTracker.Notification.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EVMTracker.Notification.Infrastructure.Persistence;

internal sealed class TransferNotificationStore(NotificationDbContext dbContext)
    : ITransferNotificationStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<int> PersistAsync(
        TransferIntegrationEventEnvelope envelope,
        IReadOnlyCollection<PreparedTransferNotification> notifications,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(notifications);

        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (await dbContext.InboxMessages.AnyAsync(
                message => message.EventId == envelope.EventId,
                cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        PreparedTransferNotification? sample = notifications.FirstOrDefault();
        if (sample is not null && await dbContext.OrphanedTransfers.AnyAsync(
                orphan =>
                    orphan.ChainId == sample.Notification.ChainId &&
                    orphan.TransactionHash == sample.Notification.TransactionHash &&
                    orphan.LogIndex == sample.Notification.LogIndex &&
                    orphan.FormerBlockHash == sample.Notification.BlockHash,
                cancellationToken).ConfigureAwait(false))
        {
            dbContext.InboxMessages.Add(
                new InboxMessage
                {
                    EventId = envelope.EventId,
                    EventType = envelope.EventType,
                    ProcessedAtUtc = envelope.OccurredAt,
                });
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        Guid[] notificationIds = notifications
            .Select(item => item.Notification.Id)
            .Distinct()
            .ToArray();
        TransferNotificationRecord[] existingRecords = notificationIds.Length == 0
            ? []
            : await dbContext.TransferNotifications
                .Where(record => notificationIds.Contains(record.Id))
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var existing = existingRecords.ToDictionary(record => record.Id);

        int inserted = 0;
        foreach (PreparedTransferNotification prepared in notifications)
        {
            TransferNotification notification = prepared.Notification;
            if (existing.TryGetValue(
                    notification.Id,
                    out TransferNotificationRecord? existingRecord) &&
                !existingRecord.IsOrphaned)
            {
                continue;
            }

            if (existingRecord is null)
            {
                existingRecord = ToRecord(notification, prepared.DeliveryRequest.EventId);
                dbContext.TransferNotifications.Add(existingRecord);
                existing.Add(notification.Id, existingRecord);
            }
            else
            {
                ApplyNotification(
                    existingRecord,
                    notification,
                    prepared.DeliveryRequest.EventId);
            }

            dbContext.OutboxMessages.Add(
                new OutboxMessage
                {
                    Id = prepared.DeliveryRequest.EventId,
                    EventType = prepared.DeliveryRequest.EventType,
                    EventVersion = prepared.DeliveryRequest.EventVersion,
                    OccurredAtUtc = prepared.DeliveryRequest.OccurredAtUtc,
                    CorrelationId = envelope.CorrelationId,
                    CausationId = envelope.EventId,
                    Payload = JsonSerializer.Serialize(
                        prepared.DeliveryRequest,
                        SerializerOptions),
                });
            inserted++;
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
        return inserted;
    }

    private static TransferNotificationRecord ToRecord(
        TransferNotification notification,
        Guid deliveryEventId) =>
        new()
        {
            Id = notification.Id,
            SubscriptionId = notification.SubscriptionId,
            SourceEventId = notification.SourceEventId,
            DeliveryEventId = deliveryEventId,
            TelegramUserId = notification.TelegramUserId,
            TelegramChatId = notification.TelegramChatId,
            ChainId = notification.ChainId,
            NetworkName = notification.NetworkName,
            MonitoredAddress = notification.MonitoredAddress,
            Label = notification.Label,
            Direction = notification.Direction,
            TokenContract = notification.TokenContract,
            TokenSymbol = notification.TokenSymbol,
            TokenName = notification.TokenName,
            TokenDecimals = notification.TokenDecimals,
            RawValue = notification.RawValue,
            DisplayAmount = notification.DisplayAmount,
            FromAddress = notification.FromAddress,
            ToAddress = notification.ToAddress,
            TransactionHash = notification.TransactionHash,
            BlockNumber = notification.BlockNumber,
            BlockHash = notification.BlockHash,
            LogIndex = notification.LogIndex,
            CreatedAtUtc = notification.CreatedAtUtc,
        };

    private static void ApplyNotification(
        TransferNotificationRecord record,
        TransferNotification notification,
        Guid deliveryEventId)
    {
        record.SourceEventId = notification.SourceEventId;
        record.DeliveryEventId = deliveryEventId;
        record.TelegramUserId = notification.TelegramUserId;
        record.TelegramChatId = notification.TelegramChatId;
        record.NetworkName = notification.NetworkName;
        record.MonitoredAddress = notification.MonitoredAddress;
        record.Label = notification.Label;
        record.Direction = notification.Direction;
        record.TokenContract = notification.TokenContract;
        record.TokenSymbol = notification.TokenSymbol;
        record.TokenName = notification.TokenName;
        record.TokenDecimals = notification.TokenDecimals;
        record.RawValue = notification.RawValue;
        record.DisplayAmount = notification.DisplayAmount;
        record.FromAddress = notification.FromAddress;
        record.ToAddress = notification.ToAddress;
        record.BlockNumber = notification.BlockNumber;
        record.BlockHash = notification.BlockHash;
        record.CreatedAtUtc = notification.CreatedAtUtc;
        record.IsOrphaned = false;
        record.OrphanedAtUtc = null;
    }
}
