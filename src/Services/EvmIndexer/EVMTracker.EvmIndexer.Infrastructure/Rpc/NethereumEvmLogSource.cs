using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Application.Decoding;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using EVMTracker.Observability;
using Microsoft.Extensions.Options;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace EVMTracker.EvmIndexer.Infrastructure.Rpc;

internal sealed class NethereumEvmLogSource : IEvmLogSource
{
    private const int MaximumRequestAttempts = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(250);

    private readonly Dictionary<long, Web3> clients;
    private readonly TimeSpan requestTimeout;

    public NethereumEvmLogSource(IOptions<EvmIndexerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        requestTimeout = options.Value.RpcRequestTimeout;
        clients = options.Value.Networks.ToDictionary(
            network => network.ChainId,
            network => new Web3(network.RpcUrl));
    }

    public async Task<long> GetHeadBlockNumberAsync(
        long chainId,
        CancellationToken cancellationToken)
    {
        HexBigInteger head = await ExecuteRpcAsync(
            chainId,
            "eth_blockNumber",
            () => GetClient(chainId)
                .Eth.Blocks.GetBlockNumber.SendRequestAsync()
                .WaitAsync(requestTimeout, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return checked((long)head.Value);
    }

    public async Task<string> GetBlockHashAsync(
        long chainId,
        long blockNumber,
        CancellationToken cancellationToken)
    {
        BlockWithTransactionHashes block = await ExecuteRpcAsync(
            chainId,
            "eth_getBlockByNumber",
            () => GetClient(chainId)
                .Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new BlockParameter(new HexBigInteger(blockNumber)))
                .WaitAsync(requestTimeout, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(block.BlockHash)
            ? throw new InvalidOperationException(
                $"RPC returned no hash for block {blockNumber} on chain {chainId}.")
            : block.BlockHash.ToLowerInvariant();
    }

    public async Task<IReadOnlyList<RawEvmLog>> GetTransferLogsAsync(
        long chainId,
        long fromBlock,
        long toBlock,
        IReadOnlyCollection<string> trackedAddresses,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trackedAddresses);
        string[] addressTopics = trackedAddresses
            .Select(ToAddressTopic)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (addressTopics.Length == 0)
        {
            return [];
        }

        var outgoingFilter = new NewFilterInput
        {
            FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
            ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
            Topics = [Erc20TransferLogDecoder.TransferTopic, addressTopics],
        };
        var incomingFilter = new NewFilterInput
        {
            FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
            ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
            Topics = [Erc20TransferLogDecoder.TransferTopic, null!, addressTopics],
        };

        try
        {
            Task<FilterLog[]> outgoing = ReadLogsAsync(
                chainId,
                outgoingFilter,
                cancellationToken);
            Task<FilterLog[]> incoming = ReadLogsAsync(
                chainId,
                incomingFilter,
                cancellationToken);
            await Task.WhenAll(outgoing, incoming).ConfigureAwait(false);
            return outgoing.Result
                .Concat(incoming.Result)
                .DistinctBy(log => new
                {
                    TransactionHash = log.TransactionHash.ToLowerInvariant(),
                    LogIndex = log.LogIndex.Value,
                    BlockHash = log.BlockHash.ToLowerInvariant(),
                })
                .OrderBy(log => log.BlockNumber.Value)
                .ThenBy(log => log.LogIndex.Value)
                .Select(MapLog)
                .ToArray();
        }
        catch (TimeoutException exception)
        {
            throw new EvmRpcRangeLimitException(
                $"The RPC request for blocks {fromBlock}-{toBlock} timed out.",
                exception);
        }
        catch (RpcResponseException exception) when (IsRangeLimitResponse(exception))
        {
            throw new EvmRpcRangeLimitException(
                $"The RPC provider rejected blocks {fromBlock}-{toBlock}.",
                exception);
        }
    }

    private Task<FilterLog[]> ReadLogsAsync(
        long chainId,
        NewFilterInput filter,
        CancellationToken cancellationToken) =>
        ExecuteRpcAsync(
            chainId,
            "eth_getLogs",
            () => GetClient(chainId)
                .Eth.Filters.GetLogs.SendRequestAsync(filter)
                .WaitAsync(requestTimeout, cancellationToken),
            cancellationToken);

    private static string ToAddressTopic(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        string normalized = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? address[2..]
            : address;
        if (normalized.Length != 40 || !normalized.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Tracked addresses must be 20-byte EVM addresses.", nameof(address));
        }

        return $"0x{normalized.PadLeft(64, '0').ToLowerInvariant()}";
    }

    private static RawEvmLog MapLog(FilterLog log) =>
        new(
            log.Address,
            log.Topics
                .Select(topic => Convert.ToString(topic, CultureInfo.InvariantCulture) ?? string.Empty)
                .ToArray(),
            log.Data,
            log.TransactionHash,
            checked((long)log.BlockNumber.Value),
            log.BlockHash,
            checked((long)log.LogIndex.Value),
            log.Removed);

    private static bool IsRangeLimitResponse(RpcResponseException exception)
    {
        string message = exception.Message;
        return message.Contains("range", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("too many", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("more than", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("response size", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("limit", StringComparison.OrdinalIgnoreCase) ||
            exception.RpcError.Code == -32005;
    }

    private Web3 GetClient(long chainId) => clients.TryGetValue(chainId, out Web3? client)
        ? client
        : throw new InvalidOperationException($"No RPC client is configured for chain {chainId}.");

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Every provider failure must update the RPC failure metric before being rethrown.")]
    private static async Task<T> ExecuteRpcAsync<T>(
        long chainId,
        string method,
        Func<Task<T>> request,
        CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();
        bool failed = false;
        using Activity? activity = EVMTrackerTelemetry.StartActivity(
            $"evm.rpc {method}",
            ActivityKind.Client);
        activity?.SetTag("rpc.system", "ethereum-json-rpc");
        activity?.SetTag("rpc.method", method);
        activity?.SetTag("evm.chain.id", chainId);
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await request().ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    attempt < MaximumRequestAttempts && IsTransient(exception))
                {
                    activity?.AddEvent(
                        new ActivityEvent(
                            "rpc.retry",
                            tags: new ActivityTagsCollection
                            {
                                ["rpc.retry.attempt"] = attempt,
                                ["exception.type"] = exception.GetType().Name,
                            }));
                    TimeSpan delay = InitialRetryDelay * attempt;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failed = true;
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
        finally
        {
            EVMTrackerTelemetry.RecordRpcRequest(
                chainId,
                method,
                Stopwatch.GetElapsedTime(startedAt),
                failed);
        }
    }

    private static bool IsTransient(Exception exception) => exception is
        RpcClientUnknownException or
        HttpRequestException or
        TimeoutException;
}
