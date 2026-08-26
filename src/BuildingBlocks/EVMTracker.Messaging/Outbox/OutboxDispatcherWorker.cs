using EVMTracker.Messaging.Configuration;
using EVMTracker.Messaging.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EVMTracker.Messaging;

internal sealed class OutboxDispatcherWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxDispatchOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Guid, int, bool, Exception?> LogDispatchFailure =
        LoggerMessage.Define<Guid, int, bool>(
            LogLevel.Warning,
            new EventId(1, nameof(LogDispatchFailure)),
            "Outbox event {EventId} delivery attempt {Attempt} failed; dead-lettered: {DeadLettered}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool dispatchedAny = await DispatchBatchAsync(stoppingToken).ConfigureAwait(false);
            if (!dispatchedAny)
            {
                await Task.Delay(
                    options.Value.PollInterval,
                    timeProvider,
                    stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IOutboxBatchStore store = scope.ServiceProvider.GetRequiredService<IOutboxBatchStore>();
        IIntegrationEventPublisher publisher =
            scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        DateTimeOffset now = timeProvider.GetUtcNow();
        IReadOnlyList<OutboxDispatchItem> messages = await store.GetPendingAsync(
            options.Value.BatchSize,
            now,
            cancellationToken).ConfigureAwait(false);

        foreach (OutboxDispatchItem message in messages)
        {
            try
            {
                await publisher.PublishAsync(
                    IntegrationEventEnvelopeFactory.Create(message),
                    cancellationToken).ConfigureAwait(false);
                await store.MarkPublishedAsync(
                    message.Id,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                int attempts = message.DeliveryAttempts + 1;
                bool deadLetter = attempts >= options.Value.MaximumDeliveryAttempts;
                DateTimeOffset failedAt = timeProvider.GetUtcNow();
                await store.MarkFailedAsync(
                    message.Id,
                    exception.Message,
                    deadLetter ? failedAt : failedAt + CalculateRetryDelay(attempts),
                    deadLetter,
                    cancellationToken).ConfigureAwait(false);
                LogDispatchFailure(
                    logger,
                    message.Id,
                    attempts,
                    deadLetter,
                    exception);
            }
        }

        return messages.Count > 0;
    }

    private TimeSpan CalculateRetryDelay(int attempts)
    {
        double multiplier = Math.Pow(2, Math.Max(0, attempts - 1));
        double milliseconds = Math.Min(
            options.Value.InitialRetryDelay.TotalMilliseconds * multiplier,
            options.Value.MaximumRetryDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
