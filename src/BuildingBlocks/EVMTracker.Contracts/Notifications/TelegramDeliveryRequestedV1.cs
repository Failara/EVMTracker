namespace EVMTracker.Contracts.Notifications;

public sealed record TelegramDeliveryRequestedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid NotificationId,
    long TelegramChatId,
    string Message,
    string ParseMode) : IIntegrationEvent
{
    public string EventType => NotificationEventTypes.TelegramDeliveryRequestedV1;

    public int EventVersion => 1;
}
