using System.Diagnostics.CodeAnalysis;
using EVMTracker.Notification.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EVMTracker.Notification.Infrastructure.Persistence;

internal sealed partial class NotificationDataRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationDataRetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<NotificationDataRetentionWorker> logger) : BackgroundService
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Retention failures must not terminate notification processing.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                NotificationRetentionResult result = await scope.ServiceProvider
                    .GetRequiredService<NotificationDataRetentionService>()
                    .PruneAsync(stoppingToken).ConfigureAwait(false);
                if (result.OutboxMessages > 0 ||
                    result.InboxMessages > 0 ||
                    result.TransferNotifications > 0 ||
                    result.OrphanedTransfers > 0)
                {
                    LogPruned(
                        logger,
                        result.OutboxMessages,
                        result.InboxMessages,
                        result.TransferNotifications,
                        result.OrphanedTransfers);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogFailure(logger, exception.GetType().Name);
            }

            await Task.Delay(
                options.Value.CleanupInterval,
                timeProvider,
                stoppingToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message = "Notification retention pruned {OutboxCount} outbox rows, {InboxCount} inbox rows, {NotificationCount} notification rows, and {OrphanCount} orphan rows.")]
    private static partial void LogPruned(
        ILogger logger,
        int outboxCount,
        int inboxCount,
        int notificationCount,
        int orphanCount);

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Warning,
        Message = "Notification retention failed with {ExceptionType}; it will retry after the cleanup interval.")]
    private static partial void LogFailure(ILogger logger, string exceptionType);
}
