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

        Guid[] notificationIds = notifications
            .Select(item => item.Notification.Id)
            .Distinct()
            .ToArray();
        Guid[] existingIds = notificationIds.Length == 0
            ? []
            : await dbContext.TransferNotifications
                .AsNoTracking()
                .Where(record => notificationIds.Contains(record.Id))
                .Select(record => record.Id)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var existing = existingIds.ToHashSet();

        int inserted = 0;
        foreach (PreparedTransferNotification prepared in notifications)
        {
            TransferNotification notification = prepared.Notification;
            if (!existing.Add(notification.Id))
            {
                continue;
            }

            dbContext.TransferNotifications.Add(ToRecord(notification));
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

    private static TransferNotificationRecord ToRecord(TransferNotification notification) =>
        new()
        {
            Id = notification.Id,
            SubscriptionId = notification.SubscriptionId,
            SourceEventId = notification.SourceEventId,
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
}
