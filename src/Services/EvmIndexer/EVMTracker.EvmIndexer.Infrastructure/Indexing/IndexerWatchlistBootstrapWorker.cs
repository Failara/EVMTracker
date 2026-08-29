using System.Diagnostics.CodeAnalysis;
using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EVMTracker.EvmIndexer.Infrastructure.Indexing;

internal sealed partial class IndexerWatchlistBootstrapWorker(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<EvmIndexerOptions> indexerOptions,
    IOptions<IndexerWatchlistBootstrapOptions> bootstrapOptions,
    TimeProvider timeProvider,
    ILogger<IndexerWatchlistBootstrapWorker> logger) : BackgroundService
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Watchlist replay remains retryable while dependent services start.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ITrackedAddressFilter filter = scope.ServiceProvider
            .GetRequiredService<ITrackedAddressFilter>();
        foreach (EvmNetworkOptions network in indexerOptions.Value.Networks)
        {
            IReadOnlyCollection<string> addresses = await filter.ListAsync(
                network.ChainId,
                stoppingToken).ConfigureAwait(false);
            if (addresses.Count > 0)
            {
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                HttpClient client = httpClientFactory.CreateClient("watchlist-bootstrap");
                using HttpResponseMessage response = await client.PostAsync(
                    "api/watchlist/events/replay",
                    content: null,
                    stoppingToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                LogReplayRequested(logger);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogReplayFailed(logger, exception.GetType().Name);
                await Task.Delay(
                    bootstrapOptions.Value.RetryInterval,
                    timeProvider,
                    stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(
        EventId = 30,
        Level = LogLevel.Information,
        Message = "Requested a watchlist event replay because the indexer projection was empty.")]
    private static partial void LogReplayRequested(ILogger logger);

    [LoggerMessage(
        EventId = 31,
        Level = LogLevel.Warning,
        Message = "Watchlist replay request failed with {ExceptionType}; it will be retried.")]
    private static partial void LogReplayFailed(ILogger logger, string exceptionType);
}
