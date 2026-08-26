using EVMTracker.Contracts.Notifications;
using EVMTracker.Notification.Domain.Notifications;

namespace EVMTracker.Notification.Application.Transfers;

public sealed record PreparedTransferNotification(
    TransferNotification Notification,
    TelegramDeliveryRequestedV1 DeliveryRequest);
