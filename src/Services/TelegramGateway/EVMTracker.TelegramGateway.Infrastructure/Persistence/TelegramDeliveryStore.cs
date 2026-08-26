using EVMTracker.Contracts.Notifications;
using EVMTracker.TelegramGateway.Application.Delivery;
using EVMTracker.TelegramGateway.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.TelegramGateway.Infrastructure.Persistence;

internal sealed class TelegramDeliveryStore(TelegramGatewayDbContext dbContext)
    : ITelegramDeliveryStore
{
    public async Task<TelegramDeliveryState> RegisterAsync(
        TelegramDeliveryRequestedV1 request,
        DateTimeOffset registeredAtUtc,
        CancellationToken cancellationToken)
    {
        TelegramDeliveryRecord? existing = await dbContext.TelegramDeliveries
            .SingleOrDefaultAsync(
                delivery => delivery.EventId == request.EventId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await ToStateAsync(existing, cancellationToken).ConfigureAwait(false);
        }

        var delivery = new TelegramDeliveryRecord
        {
            EventId = request.EventId,
            NotificationId = request.NotificationId,
            TelegramChatId = request.TelegramChatId,
            Message = request.Message,
            ParseMode = request.ParseMode,
            Status = TelegramDeliveryStatus.Pending,
            RequestedAtUtc = request.OccurredAtUtc,
            UpdatedAtUtc = registeredAtUtc,
        };
        dbContext.TelegramDeliveries.Add(delivery);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TelegramDeliveryState(
            delivery.EventId,
            delivery.Status,
            delivery.AttemptCount);
    }

    public async Task<int> BeginAttemptAsync(
        Guid eventId,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken)
    {
        TelegramDeliveryRecord delivery = await FindDeliveryAsync(
            eventId,
            cancellationToken).ConfigureAwait(false);
        TelegramDeliveryAttemptRecord? incomplete =
            await dbContext.TelegramDeliveryAttempts.SingleOrDefaultAsync(
                attempt => attempt.EventId == eventId &&
                    attempt.CompletedAtUtc == null,
                cancellationToken).ConfigureAwait(false);
        if (incomplete is not null)
        {
            return incomplete.AttemptNumber;
        }

        delivery.AttemptCount++;
        delivery.UpdatedAtUtc = attemptedAtUtc;
        dbContext.TelegramDeliveryAttempts.Add(
            new TelegramDeliveryAttemptRecord
            {
                EventId = eventId,
                AttemptNumber = delivery.AttemptCount,
                AttemptedAtUtc = attemptedAtUtc,
            });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return delivery.AttemptCount;
    }

    public async Task CompleteAttemptAsync(
        Guid eventId,
        int attemptNumber,
        TelegramDeliveryAttemptOutcome outcome,
        int? telegramMessageId,
        string? failureReason,
        TimeSpan? retryDelay,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        TelegramDeliveryRecord delivery = await FindDeliveryAsync(
            eventId,
            cancellationToken).ConfigureAwait(false);
        TelegramDeliveryAttemptRecord attempt = await dbContext.TelegramDeliveryAttempts
            .SingleAsync(
                item => item.EventId == eventId &&
                    item.AttemptNumber == attemptNumber,
                cancellationToken).ConfigureAwait(false);
        attempt.CompletedAtUtc = completedAtUtc;
        attempt.Outcome = outcome;
        attempt.TelegramMessageId = telegramMessageId;
        attempt.Error = Truncate(failureReason, 2000);
        attempt.RetryDelayMilliseconds = retryDelay is null
            ? null
            : checked((long)retryDelay.Value.TotalMilliseconds);

        delivery.UpdatedAtUtc = completedAtUtc;
        delivery.TelegramMessageId = telegramMessageId;
        delivery.LastError = Truncate(failureReason, 2000);
        delivery.Status = outcome switch
        {
            TelegramDeliveryAttemptOutcome.Delivered => TelegramDeliveryStatus.Delivered,
            TelegramDeliveryAttemptOutcome.TransientFailure =>
                TelegramDeliveryStatus.RetryScheduled,
            TelegramDeliveryAttemptOutcome.PermanentFailure =>
                TelegramDeliveryStatus.PermanentlyFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
        delivery.DeliveredAtUtc = outcome == TelegramDeliveryAttemptOutcome.Delivered
            ? completedAtUtc
            : null;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<TelegramDeliveryRecord> FindDeliveryAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        await dbContext.TelegramDeliveries.SingleOrDefaultAsync(
            delivery => delivery.EventId == eventId,
            cancellationToken).ConfigureAwait(false) ??
        throw new InvalidOperationException($"Telegram delivery {eventId} is not registered.");

    private async Task<TelegramDeliveryState> ToStateAsync(
        TelegramDeliveryRecord delivery,
        CancellationToken cancellationToken)
    {
        bool hasIncompleteAttempt = await dbContext.TelegramDeliveryAttempts.AnyAsync(
            attempt => attempt.EventId == delivery.EventId &&
                attempt.CompletedAtUtc == null,
            cancellationToken).ConfigureAwait(false);
        return new TelegramDeliveryState(
            delivery.EventId,
            delivery.Status,
            delivery.AttemptCount,
            hasIncompleteAttempt);
    }

    private static string? Truncate(string? value, int maximumLength) =>
        value is not null && value.Length > maximumLength
            ? value[..maximumLength]
            : value;
}
