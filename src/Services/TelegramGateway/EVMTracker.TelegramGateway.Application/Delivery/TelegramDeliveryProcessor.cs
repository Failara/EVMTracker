using EVMTracker.Contracts.Notifications;

namespace EVMTracker.TelegramGateway.Application.Delivery;

public sealed class TelegramDeliveryProcessor(
    ITelegramDeliveryStore store,
    ITelegramMessageSender sender,
    ITelegramDeliveryRateLimiter rateLimiter,
    IDeliveryDelay delay,
    TelegramDeliveryPolicy policy,
    TimeProvider timeProvider)
{
    public async Task<TelegramDeliveryProcessingResult> ProcessAsync(
        TelegramDeliveryRequestedV1 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        TelegramDeliveryState state = await store.RegisterAsync(
            request,
            timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false);
        if (state.Status == TelegramDeliveryStatus.Delivered)
        {
            return TelegramDeliveryProcessingResult.AlreadyDelivered;
        }

        int nextAttempt = state.HasIncompleteAttempt
            ? state.AttemptCount
            : state.AttemptCount + 1;
        if (state.Status == TelegramDeliveryStatus.PermanentlyFailed ||
            nextAttempt > policy.MaximumAttempts)
        {
            throw new TelegramDeliveryAttemptsExhaustedException(
                $"Telegram delivery {request.EventId} has no remaining attempts.");
        }

        for (int attempt = nextAttempt;
             attempt <= policy.MaximumAttempts;
             attempt++)
        {
            await rateLimiter.WaitAsync(
                request.TelegramChatId,
                cancellationToken).ConfigureAwait(false);
            int attemptNumber = await store.BeginAttemptAsync(
                request.EventId,
                timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
            TelegramSendResult result = await sender.SendAsync(
                request.TelegramChatId,
                request.Message,
                request.ParseMode,
                cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                await store.CompleteAttemptAsync(
                    request.EventId,
                    attemptNumber,
                    TelegramDeliveryAttemptOutcome.Delivered,
                    result.TelegramMessageId,
                    null,
                    null,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);
                return TelegramDeliveryProcessingResult.Delivered;
            }

            if (result.IsPermanentFailure)
            {
                await store.CompleteAttemptAsync(
                    request.EventId,
                    attemptNumber,
                    TelegramDeliveryAttemptOutcome.PermanentFailure,
                    null,
                    result.Error,
                    null,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);
                throw new PermanentTelegramDeliveryException(
                    result.Error ?? "Telegram rejected the delivery request permanently.");
            }

            bool exhausted = attemptNumber >= policy.MaximumAttempts;
            TimeSpan? retryDelay = exhausted
                ? null
                : TelegramRetryDelayCalculator.Calculate(
                    request.EventId,
                    attemptNumber,
                    result.RetryAfter,
                    policy);
            await store.CompleteAttemptAsync(
                request.EventId,
                attemptNumber,
                exhausted
                    ? TelegramDeliveryAttemptOutcome.PermanentFailure
                    : TelegramDeliveryAttemptOutcome.TransientFailure,
                null,
                result.Error,
                retryDelay,
                timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
            if (exhausted)
            {
                throw new TelegramDeliveryAttemptsExhaustedException(
                    result.Error ?? $"Telegram delivery {request.EventId} exhausted its retries.");
            }

            await delay.WaitAsync(
                retryDelay!.Value,
                cancellationToken).ConfigureAwait(false);
        }

        throw new TelegramDeliveryAttemptsExhaustedException(
            $"Telegram delivery {request.EventId} exhausted its retries.");
    }
}
