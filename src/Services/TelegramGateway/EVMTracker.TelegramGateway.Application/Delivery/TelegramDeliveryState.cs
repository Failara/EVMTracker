namespace EVMTracker.TelegramGateway.Application.Delivery;

public sealed record TelegramDeliveryState(
    Guid EventId,
    TelegramDeliveryStatus Status,
    int AttemptCount,
    bool HasIncompleteAttempt = false);
