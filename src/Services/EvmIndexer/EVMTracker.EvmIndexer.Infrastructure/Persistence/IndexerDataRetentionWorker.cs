using System.Diagnostics.CodeAnalysis;
using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence;

internal sealed partial class IndexerDataRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IndexerDataRetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<IndexerDataRetentionWorker> logger) : BackgroundService
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Retention failures must not terminate blockchain indexing.")]
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
                IndexerRetentionResult result = await scope.ServiceProvider
                    .GetRequiredService<IndexerDataRetentionService>()
                    .PruneAsync(stoppingToken).ConfigureAwait(false);
                if (result.OutboxMessages > 0 ||
                    result.ProcessedTransfers > 0 ||
                    result.WatchlistInboxMessages > 0)
                {
                    LogPruned(
                        logger,
                        result.OutboxMessages,
                        result.ProcessedTransfers,
                        result.WatchlistInboxMessages);
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
        Message = "Indexer retention pruned {OutboxCount} outbox rows, {TransferCount} transfer rows, and {WatchlistInboxCount} watchlist inbox rows.")]
    private static partial void LogPruned(
        ILogger logger,
        int outboxCount,
        int transferCount,
        int watchlistInboxCount);

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Warning,
        Message = "Indexer retention failed with {ExceptionType}; it will retry after the cleanup interval.")]
    private static partial void LogFailure(ILogger logger, string exceptionType);
}
