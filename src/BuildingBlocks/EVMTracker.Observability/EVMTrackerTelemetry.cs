using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EVMTracker.Observability;

public static class EVMTrackerTelemetry
{
    public const string ActivitySourceName = "EVMTracker";
    public const string MeterName = "EVMTracker";

    private static readonly ActivitySource Activities = new(ActivitySourceName);
    private static readonly Meter Metrics = new(MeterName);
    private static readonly Counter<long> DecodedLogs = Metrics.CreateCounter<long>(
        "evmtracker.indexer.logs.decoded",
        description: "Number of valid ERC-20 Transfer logs decoded by the indexer.");
    private static readonly Counter<long> MalformedLogs = Metrics.CreateCounter<long>(
        "evmtracker.indexer.logs.malformed",
        description: "Number of malformed EVM logs rejected by the decoder.");
    private static readonly Histogram<double> RpcDuration = Metrics.CreateHistogram<double>(
        "evmtracker.indexer.rpc.duration",
        unit: "s",
        description: "EVM JSON-RPC request duration.");
    private static readonly Counter<long> RpcFailures = Metrics.CreateCounter<long>(
        "evmtracker.indexer.rpc.failures",
        description: "Number of failed EVM JSON-RPC requests.");
    private static readonly Counter<long> Reorganizations = Metrics.CreateCounter<long>(
        "evmtracker.indexer.reorganizations",
        description: "Number of EVM reorganizations detected.");
    private static readonly Counter<long> WatchlistMatches = Metrics.CreateCounter<long>(
        "evmtracker.notification.watchlist.matches",
        description: "Number of projected watchlist subscriptions matching transfers.");
    private static readonly Counter<long> CreatedNotifications = Metrics.CreateCounter<long>(
        "evmtracker.notification.created",
        description: "Number of logical transfer notifications created.");
    private static readonly Counter<long> TelegramDeliveries = Metrics.CreateCounter<long>(
        "evmtracker.telegram.deliveries",
        description: "Number of Telegram delivery consumer outcomes.");
    private static readonly Counter<long> OutboxPublished = Metrics.CreateCounter<long>(
        "evmtracker.messaging.outbox.published",
        description: "Number of outbox events published.");
    private static readonly Counter<long> OutboxFailures = Metrics.CreateCounter<long>(
        "evmtracker.messaging.outbox.failures",
        description: "Number of outbox publication failures.");
    private static readonly ConcurrentDictionary<long, IndexerProgress> IndexerStates = new();

    static EVMTrackerTelemetry()
    {
        Metrics.CreateObservableGauge(
            "evmtracker.indexer.chain.head",
            ObserveChainHeads,
            description: "Current EVM chain head by chain ID.");
        Metrics.CreateObservableGauge(
            "evmtracker.indexer.last.processed.block",
            ObserveLastProcessedBlocks,
            description: "Last confirmed block processed by the indexer.");
        Metrics.CreateObservableGauge(
            "evmtracker.indexer.chain.lag",
            ObserveChainLag,
            description: "Blocks between the current chain head and last processed block.");
    }

    public static Activity? StartIntegrationEventActivity(
        string operation,
        string eventType,
        Guid eventId,
        Guid correlationId,
        Guid? causationId,
        ActivityKind kind)
    {
        Activity? activity = Activities.StartActivity(
            $"messaging {operation} {eventType}",
            kind);
        activity?.SetTag("messaging.operation.name", operation);
        activity?.SetTag("messaging.message.type", eventType);
        activity?.SetTag("messaging.message.id", eventId.ToString("D"));
        activity?.SetTag("correlation.id", correlationId.ToString("D"));
        if (causationId.HasValue)
        {
            activity?.SetTag("causation.id", causationId.Value.ToString("D"));
        }

        return activity;
    }

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal) =>
        Activities.StartActivity(name, kind);

    public static void SetIndexerProgress(
        long chainId,
        long chainHead,
        long lastProcessedBlock)
    {
        IndexerStates[chainId] = new IndexerProgress(chainHead, lastProcessedBlock);
    }

    public static void RecordIndexerIteration(
        long chainId,
        int decodedLogCount,
        int malformedLogCount,
        bool reorganized)
    {
        TagList tags = default;
        tags.Add("evm.chain.id", chainId);
        DecodedLogs.Add(decodedLogCount, tags);
        MalformedLogs.Add(malformedLogCount, tags);
        if (reorganized)
        {
            Reorganizations.Add(1, tags);
        }
    }

    public static void RecordRpcRequest(
        long chainId,
        string method,
        TimeSpan duration,
        bool failed)
    {
        TagList tags = default;
        tags.Add("evm.chain.id", chainId);
        tags.Add("rpc.method", method);
        RpcDuration.Record(duration.TotalSeconds, tags);
        if (failed)
        {
            RpcFailures.Add(1, tags);
        }
    }

    public static void RecordNotificationMatch(int matches, int created)
    {
        WatchlistMatches.Add(matches);
        CreatedNotifications.Add(created);
    }

    public static void RecordTelegramDelivery(string outcome) =>
        TelegramDeliveries.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public static void RecordOutboxPublished(string eventType) =>
        OutboxPublished.Add(1, new KeyValuePair<string, object?>("event.type", eventType));

    public static void RecordOutboxFailure(string eventType, bool deadLettered)
    {
        TagList tags = default;
        tags.Add("event.type", eventType);
        tags.Add("dead_lettered", deadLettered);
        OutboxFailures.Add(1, tags);
    }

    private static IEnumerable<Measurement<long>> ObserveChainHeads() =>
        IndexerStates.Select(item => Measurement(item.Key, item.Value.ChainHead));

    private static IEnumerable<Measurement<long>> ObserveLastProcessedBlocks() =>
        IndexerStates.Select(item => Measurement(item.Key, item.Value.LastProcessedBlock));

    private static IEnumerable<Measurement<long>> ObserveChainLag() =>
        IndexerStates.Select(
            item => Measurement(
                item.Key,
                Math.Max(0, item.Value.ChainHead - item.Value.LastProcessedBlock)));

    private static Measurement<long> Measurement(long chainId, long value) =>
        new(value, new KeyValuePair<string, object?>("evm.chain.id", chainId));

    private sealed record IndexerProgress(long ChainHead, long LastProcessedBlock);
}
