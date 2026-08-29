using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using EVMTracker.EvmIndexer.Application.Indexing;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using EVMTracker.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EVMTracker.EvmIndexer.Infrastructure.Indexing;

internal sealed partial class EvmIndexerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EvmIndexerOptions> options,
    ILogger<EvmIndexerWorker> logger) : BackgroundService
{
    private readonly EvmIndexerOptions options = options.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Task[] networkTasks = options.Networks
            .Select(network => RunNetworkAsync(network, stoppingToken))
            .ToArray();
        return Task.WhenAll(networkTasks);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "One provider or persistence failure must not terminate indexing for that network.")]
    private async Task RunNetworkAsync(
        EvmNetworkOptions network,
        CancellationToken cancellationToken)
    {
        LogNetworkStarted(logger, network.Name, network.ChainId);
        var configuration = new EvmNetworkConfiguration(
            network.Name,
            network.ChainId,
            network.StartBlock,
            network.ConfirmationDepth,
            network.InitialBlockRange,
            network.MinimumBlockRange,
            network.MaximumBlockRange,
            network.ReorganizationWindow);

        while (!cancellationToken.IsCancellationRequested)
        {
            bool shouldDelay = true;
            try
            {
                using Activity? activity = EVMTrackerTelemetry.StartActivity(
                    "evm.indexer.iteration");
                activity?.SetTag("evm.chain.id", network.ChainId);
                activity?.SetTag("evm.network", network.Name);
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                EvmIndexerProcessor processor = scope.ServiceProvider
                    .GetRequiredService<EvmIndexerProcessor>();
                IndexerRunResult result = await processor.ProcessOnceAsync(
                    configuration,
                    cancellationToken).ConfigureAwait(false);
                LogIterationCompleted(
                    logger,
                    network.Name,
                    result.Status,
                    result.FromBlock,
                    result.ToBlock,
                    result.PersistedTransferCount,
                    result.OrphanedTransferCount,
                    result.NextRangeSize);
                activity?.SetTag("indexer.status", result.Status.ToString());
                activity?.SetTag("indexer.from_block", result.FromBlock);
                activity?.SetTag("indexer.to_block", result.ToBlock);
                EVMTrackerTelemetry.SetIndexerProgress(
                    network.ChainId,
                    result.ChainHead,
                    result.LastProcessedBlock);
                EVMTrackerTelemetry.RecordIndexerIteration(
                    network.ChainId,
                    result.RetrievedLogCount - result.MalformedLogCount,
                    result.MalformedLogCount,
                    result.Status == IndexerRunStatus.Reorganized);
                shouldDelay = result.Status is
                    IndexerRunStatus.NoConfirmedBlocks or
                    IndexerRunStatus.NoTrackedAddresses;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogIterationFailed(
                    logger,
                    network.Name,
                    exception.GetType().Name,
                    SensitiveDataRedactor.Redact(null, exception.Message));
            }

            if (shouldDelay)
            {
                await Task.Delay(network.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Starting EVM indexing for {NetworkName} ({ChainId}).")]
    private static partial void LogNetworkStarted(
        ILogger logger,
        string networkName,
        long chainId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Indexer {NetworkName}: {Status}, blocks {FromBlock}-{ToBlock}, persisted {PersistedCount}, orphaned {OrphanedCount}, next range {NextRangeSize}.")]
    private static partial void LogIterationCompleted(
        ILogger logger,
        string networkName,
        IndexerRunStatus status,
        long? fromBlock,
        long? toBlock,
        int persistedCount,
        int orphanedCount,
        int nextRangeSize);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Indexer iteration failed for {NetworkName} with {ExceptionType}: {SafeError}; it will retry after the polling interval.")]
    private static partial void LogIterationFailed(
        ILogger logger,
        string networkName,
        string exceptionType,
        string safeError);
}
