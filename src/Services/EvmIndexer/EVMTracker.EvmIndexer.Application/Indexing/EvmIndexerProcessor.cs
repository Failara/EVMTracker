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
        IndexerRunResult? reorganization = await ReconcileCanonicalChainAsync(
            network,
            storedCheckpoint,
            currentRange,
            cancellationToken).ConfigureAwait(false);
        if (reorganization is not null)
        {
            return reorganization;
        }

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

        IReadOnlyList<CanonicalBlock> canonicalBlocks = await ReadCanonicalBlocksAsync(
            network,
            blockRange,
            indexedTransfers,
            cancellationToken).ConfigureAwait(false);
        if (!LogsMatchCanonicalChain(indexedTransfers, canonicalBlocks))
        {
            return new IndexerRunResult(
                IndexerRunStatus.ChainChangedDuringRead,
                blockRange.FromBlock,
                blockRange.ToBlock,
                logs.Count,
                0,
                malformedLogCount,
                currentRange);
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
            canonicalBlocks,
            network.ReorganizationWindow,
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
        Guid eventId = TransferEventIdentity.CreateObservedEventId(transfer);
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

    private async Task<IndexerRunResult?> ReconcileCanonicalChainAsync(
        EvmNetworkConfiguration network,
        IndexerCheckpoint? checkpoint,
        int currentRange,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CanonicalBlock> recent =
            await stateStore.GetRecentCanonicalBlocksAsync(
                network.ChainId,
                cancellationToken).ConfigureAwait(false);
        if (recent.Count == 0)
        {
            return null;
        }

        CanonicalBlock latest = recent[0];
        string latestHash = await logSource.GetBlockHashAsync(
            network.ChainId,
            latest.BlockNumber,
            cancellationToken).ConfigureAwait(false);
        if (HashesEqual(latest.BlockHash, latestHash))
        {
            return null;
        }

        CanonicalBlock? commonAncestor = null;
        foreach (CanonicalBlock candidate in recent.Skip(1))
        {
            string currentHash = await logSource.GetBlockHashAsync(
                network.ChainId,
                candidate.BlockNumber,
                cancellationToken).ConfigureAwait(false);
            if (HashesEqual(candidate.BlockHash, currentHash))
            {
                commonAncestor = candidate;
                break;
            }
        }

        long rewindBlock;
        if (commonAncestor is not null)
        {
            rewindBlock = commonAncestor.BlockNumber + 1;
        }
        else if (recent[^1].BlockNumber == network.StartBlock)
        {
            rewindBlock = network.StartBlock;
        }
        else
        {
            throw new ReorganizationWindowExceededException(
                $"Chain {network.ChainId} diverged below retained block {recent[^1].BlockNumber}.");
        }

        DateTimeOffset detectedAt = timeProvider.GetUtcNow();
        var rewoundCheckpoint = new IndexerCheckpoint(
            network.ChainId,
            rewindBlock,
            currentRange,
            detectedAt);
        ReorganizationResult result = await stateStore.RewindAsync(
            rewoundCheckpoint,
            rewindBlock,
            detectedAt,
            cancellationToken).ConfigureAwait(false);
        return new IndexerRunResult(
            IndexerRunStatus.Reorganized,
            rewindBlock,
            checkpoint is null ? null : checkpoint.NextBlockNumber - 1,
            0,
            0,
            0,
            currentRange,
            result.OrphanedTransferCount);
    }

    private async Task<IReadOnlyList<CanonicalBlock>> ReadCanonicalBlocksAsync(
        EvmNetworkConfiguration network,
        BlockRange blockRange,
        IReadOnlyCollection<IndexedTransfer> transfers,
        CancellationToken cancellationToken)
    {
        long tailFrom = Math.Max(
            blockRange.FromBlock,
            blockRange.ToBlock - network.ReorganizationWindow + 1);
        var blockNumbers = new HashSet<long>(
            Enumerable.Range(0, checked((int)(blockRange.ToBlock - tailFrom + 1)))
                .Select(offset => tailFrom + offset));
        blockNumbers.UnionWith(transfers.Select(item => item.Transfer.BlockNumber));

        Task<CanonicalBlock>[] reads = blockNumbers
            .Order()
            .Select(async blockNumber =>
                new CanonicalBlock(
                    network.ChainId,
                    blockNumber,
                    await logSource.GetBlockHashAsync(
                        network.ChainId,
                        blockNumber,
                        cancellationToken).ConfigureAwait(false)))
            .ToArray();
        CanonicalBlock[] blocks = await Task.WhenAll(reads).ConfigureAwait(false);
        return blocks;
    }

    private static bool LogsMatchCanonicalChain(
        IEnumerable<IndexedTransfer> transfers,
        IEnumerable<CanonicalBlock> canonicalBlocks)
    {
        Dictionary<long, string> hashes = canonicalBlocks.ToDictionary(
            block => block.BlockNumber,
            block => block.BlockHash);
        return transfers.All(item =>
            hashes.TryGetValue(item.Transfer.BlockNumber, out string? blockHash) &&
            HashesEqual(item.Transfer.BlockHash.Value, blockHash));
    }

    private static bool HashesEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
