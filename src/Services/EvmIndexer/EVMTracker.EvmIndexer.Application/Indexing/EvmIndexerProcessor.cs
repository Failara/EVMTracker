using System.Security.Cryptography;
using System.Text;
using EVMTracker.Contracts.Transfers;
using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Application.Decoding;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Indexing;
using EVMTracker.EvmIndexer.Domain.Transfers;

namespace EVMTracker.EvmIndexer.Application.Indexing;

public sealed class EvmIndexerProcessor(
    IEvmLogSource logSource,
    ITokenMetadataProvider tokenMetadataProvider,
    IIndexerStateStore stateStore,
    TimeProvider timeProvider)
{
    public async Task<IndexerRunResult> ProcessOnceAsync(
        EvmNetworkConfiguration network,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(network);

        IndexerCheckpoint? storedCheckpoint = await stateStore.GetCheckpointAsync(
            network.ChainId,
            cancellationToken).ConfigureAwait(false);
        long nextBlock = storedCheckpoint?.NextBlockNumber ?? network.StartBlock;
        int currentRange = storedCheckpoint?.CurrentRangeSize ?? network.InitialBlockRange;

        long chainHead = await logSource.GetHeadBlockNumberAsync(
            network.ChainId,
            cancellationToken).ConfigureAwait(false);
        long safeHead = BlockRange.CalculateSafeHead(chainHead, network.ConfirmationDepth);
        if (safeHead < nextBlock)
        {
            return new IndexerRunResult(
                IndexerRunStatus.NoConfirmedBlocks,
                null,
                null,
                0,
                0,
                0,
                currentRange);
        }

        BlockRange blockRange = BlockRange.Create(nextBlock, safeHead, currentRange);
        IReadOnlyList<RawEvmLog> logs;
        try
        {
            logs = await logSource.GetTransferLogsAsync(
                network.ChainId,
                blockRange.FromBlock,
                blockRange.ToBlock,
                cancellationToken).ConfigureAwait(false);
        }
        catch (EvmRpcRangeLimitException)
        {
            int reducedRange = Math.Max(network.MinimumBlockRange, currentRange / 2);
            await stateStore.UpdateRangeSizeAsync(
                new IndexerCheckpoint(
                    network.ChainId,
                    nextBlock,
                    reducedRange,
                    timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
            return new IndexerRunResult(
                IndexerRunStatus.RangeReduced,
                blockRange.FromBlock,
                blockRange.ToBlock,
                0,
                0,
                0,
                reducedRange);
        }

        var indexedTransfers = new List<IndexedTransfer>(logs.Count);
        int malformedLogCount = 0;
        foreach (RawEvmLog log in logs)
        {
            TransferLogDecodeResult decodeResult = Erc20TransferLogDecoder.Decode(
                network.ChainId,
                log);
            if (!decodeResult.IsSuccess)
            {
                malformedLogCount++;
                continue;
            }

            Erc20Transfer transfer = decodeResult.Transfer!;
            TokenMetadata metadata = await tokenMetadataProvider.GetAsync(
                network.ChainId,
                transfer.TokenContract,
                cancellationToken).ConfigureAwait(false);
            indexedTransfers.Add(
                new IndexedTransfer(
                    transfer,
                    metadata,
                    CreateIntegrationEvent(transfer, metadata, timeProvider.GetUtcNow())));
        }

        int nextRange = GrowRange(currentRange, network.MaximumBlockRange);
        var checkpoint = new IndexerCheckpoint(
            network.ChainId,
            blockRange.ToBlock + 1,
            nextRange,
            timeProvider.GetUtcNow());
        int persistedCount = await stateStore.CommitBatchAsync(
            checkpoint,
            indexedTransfers,
            cancellationToken).ConfigureAwait(false);

        return new IndexerRunResult(
            IndexerRunStatus.Processed,
            blockRange.FromBlock,
            blockRange.ToBlock,
            logs.Count,
            persistedCount,
            malformedLogCount,
            nextRange);
    }

    private static Erc20TransferObservedV1 CreateIntegrationEvent(
        Erc20Transfer transfer,
        TokenMetadata metadata,
        DateTimeOffset occurredAtUtc)
    {
        Guid eventId = CreateDeterministicEventId(transfer);
        return new Erc20TransferObservedV1(
            eventId,
            occurredAtUtc,
            transfer.ChainId,
            transfer.TokenContract.Value,
            transfer.FromAddress.Value,
            transfer.ToAddress.Value,
            transfer.RawValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            metadata.Symbol,
            metadata.Name,
            metadata.Decimals,
            transfer.TransactionHash.Value,
            transfer.BlockNumber,
            transfer.BlockHash.Value,
            transfer.LogIndex);
    }

    private static int GrowRange(int currentRange, int maximumRange) =>
        currentRange >= maximumRange || currentRange > maximumRange / 2
            ? maximumRange
            : currentRange * 2;

    private static Guid CreateDeterministicEventId(Erc20Transfer transfer)
    {
        byte[] identity = Encoding.UTF8.GetBytes(
            $"{transfer.ChainId}:{transfer.TransactionHash.Value}:{transfer.LogIndex}");
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(identity, hash);
        return new Guid(hash[..16]);
    }
}
