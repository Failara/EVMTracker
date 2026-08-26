using EVMTracker.Contracts.Notifications;

namespace EVMTracker.TelegramGateway.Application.Delivery;

public interface ITelegramDeliveryStore
{
    Task<TelegramDeliveryState> RegisterAsync(
        TelegramDeliveryRequestedV1 request,
        DateTimeOffset registeredAtUtc,
        CancellationToken cancellationToken);

    Task<int> BeginAttemptAsync(
        Guid eventId,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken);

    Task CompleteAttemptAsync(
        Guid eventId,
        int attemptNumber,
        TelegramDeliveryAttemptOutcome outcome,
        int? telegramMessageId,
        string? failureReason,
        TimeSpan? retryDelay,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken);
}
