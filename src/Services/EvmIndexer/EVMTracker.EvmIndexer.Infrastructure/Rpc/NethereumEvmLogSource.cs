using System.Globalization;
using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Application.Decoding;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace EVMTracker.EvmIndexer.Infrastructure.Rpc;

internal sealed class NethereumEvmLogSource : IEvmLogSource
{
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
        HexBigInteger head = await GetClient(chainId)
            .Eth.Blocks.GetBlockNumber.SendRequestAsync()
            .WaitAsync(requestTimeout, cancellationToken).ConfigureAwait(false);
        return checked((long)head.Value);
    }

    public async Task<string> GetBlockHashAsync(
        long chainId,
        long blockNumber,
        CancellationToken cancellationToken)
    {
        BlockWithTransactionHashes block = await GetClient(chainId)
            .Eth.Blocks.GetBlockWithTransactionsHashesByNumber
            .SendRequestAsync(new BlockParameter(new HexBigInteger(blockNumber)))
            .WaitAsync(requestTimeout, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(block.BlockHash)
            ? throw new InvalidOperationException(
                $"RPC returned no hash for block {blockNumber} on chain {chainId}.")
            : block.BlockHash.ToLowerInvariant();
    }

    public async Task<IReadOnlyList<RawEvmLog>> GetTransferLogsAsync(
        long chainId,
        long fromBlock,
        long toBlock,
        CancellationToken cancellationToken)
    {
        var filter = new NewFilterInput
        {
            FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
            ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
            Topics = [Erc20TransferLogDecoder.TransferTopic],
        };

        try
        {
            FilterLog[] logs = await GetClient(chainId)
                .Eth.Filters.GetLogs.SendRequestAsync(filter)
                .WaitAsync(requestTimeout, cancellationToken).ConfigureAwait(false);
            return logs.Select(MapLog).ToArray();
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
}
