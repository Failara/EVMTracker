using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Application.Indexing;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Indexing;
using EVMTracker.EvmIndexer.Domain.Transfers;

namespace EVMTracker.EvmIndexer.UnitTests;

public sealed class EvmIndexerProcessorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessesOnlyConfirmedBoundedRangeAndAdvancesCheckpoint()
    {
        var source = new FakeLogSource
        {
            Head = 120,
            Logs = [Erc20TransferLogDecoderTests.CreateTransferLog()],
        };
        var store = new FakeStateStore();
        EvmIndexerProcessor processor = CreateProcessor(source, store);

        IndexerRunResult result = await processor.ProcessOnceAsync(
            CreateConfiguration(startBlock: 100, initialRange: 5),
            CancellationToken.None);

        Assert.Equal(IndexerRunStatus.Processed, result.Status);
        Assert.Equal(100, source.LastFromBlock);
        Assert.Equal(104, source.LastToBlock);
        Assert.Equal(105, store.Checkpoint?.NextBlockNumber);
        Assert.Equal(10, store.Checkpoint?.CurrentRangeSize);
        Assert.Single(store.LastCommittedTransfers);
        Assert.Equal("USDT", store.LastCommittedTransfers[0].TokenMetadata.Symbol);
    }

    [Fact]
    public async Task RestartContinuesFromStoredNextBlock()
    {
        var source = new FakeLogSource { Head = 250, Logs = [] };
        var store = new FakeStateStore
        {
            Checkpoint = new IndexerCheckpoint(1, 200, 20, Now),
        };
        EvmIndexerProcessor processor = CreateProcessor(source, store);

        await processor.ProcessOnceAsync(CreateConfiguration(), CancellationToken.None);

        Assert.Equal(200, source.LastFromBlock);
        Assert.Equal(219, source.LastToBlock);
    }

    [Fact]
    public async Task NoConfirmedRangeDoesNotCallLogsOrAdvanceCheckpoint()
    {
        var source = new FakeLogSource { Head = 105 };
        var store = new FakeStateStore();
        EvmIndexerProcessor processor = CreateProcessor(source, store);

        IndexerRunResult result = await processor.ProcessOnceAsync(
            CreateConfiguration(startBlock: 100, confirmationDepth: 12),
            CancellationToken.None);

        Assert.Equal(IndexerRunStatus.NoConfirmedBlocks, result.Status);
        Assert.Null(source.LastFromBlock);
        Assert.Null(store.Checkpoint);
    }

    [Fact]
    public async Task ProviderRangeLimitHalvesRangeWithoutAdvancingBlock()
    {
        var source = new FakeLogSource { Head = 500, ThrowRangeLimit = true };
        var store = new FakeStateStore
        {
            Checkpoint = new IndexerCheckpoint(1, 100, 100, Now),
        };
        EvmIndexerProcessor processor = CreateProcessor(source, store);

        IndexerRunResult result = await processor.ProcessOnceAsync(
            CreateConfiguration(minimumRange: 10),
            CancellationToken.None);

        Assert.Equal(IndexerRunStatus.RangeReduced, result.Status);
        Assert.Equal(100, store.Checkpoint?.NextBlockNumber);
        Assert.Equal(50, store.Checkpoint?.CurrentRangeSize);
    }

    [Fact]
    public async Task TransferEventIdentityIsStableAcrossReprocessing()
    {
        var source = new FakeLogSource
        {
            Head = 120,
            Logs = [Erc20TransferLogDecoderTests.CreateTransferLog()],
        };
        var firstStore = new FakeStateStore();
        var secondStore = new FakeStateStore();

        await CreateProcessor(source, firstStore).ProcessOnceAsync(
            CreateConfiguration(startBlock: 100),
            CancellationToken.None);
        await CreateProcessor(source, secondStore).ProcessOnceAsync(
            CreateConfiguration(startBlock: 100),
            CancellationToken.None);

        Assert.Equal(
            firstStore.LastCommittedTransfers[0].IntegrationEvent.EventId,
            secondStore.LastCommittedTransfers[0].IntegrationEvent.EventId);
    }

    [Fact]
    public async Task UnknownTokenMetadataStillProducesTransferEvent()
    {
        var source = new FakeLogSource
        {
            Head = 120,
            Logs = [Erc20TransferLogDecoderTests.CreateTransferLog()],
        };
        var store = new FakeStateStore();
        var metadataProvider = new FakeTokenMetadataProvider
        {
            Metadata = TokenMetadata.Unknown(
                "0x9999999999999999999999999999999999999999"),
        };
        var processor = new EvmIndexerProcessor(
            source,
            metadataProvider,
            store,
            new FixedTimeProvider(Now));

        await processor.ProcessOnceAsync(
            CreateConfiguration(startBlock: 100),
            CancellationToken.None);

        Assert.StartsWith(
            "TOKEN-",
            store.LastCommittedTransfers[0].IntegrationEvent.TokenSymbol,
            StringComparison.Ordinal);
        Assert.Null(store.LastCommittedTransfers[0].IntegrationEvent.TokenDecimals);
    }

    [Fact]
    public async Task ShortReorganizationRewindsToBlockAfterCommonAncestor()
    {
        var source = new FakeLogSource { Head = 120, Logs = [] };
        source.BlockHashes[103] = Hash('a');
        source.BlockHashes[104] = Hash('c');
        var store = new FakeStateStore
        {
            Checkpoint = new IndexerCheckpoint(1, 105, 10, Now),
            CanonicalBlocks =
            [
                new CanonicalBlock(1, 104, Hash('b')),
                new CanonicalBlock(1, 103, Hash('a')),
                new CanonicalBlock(1, 102, Hash('9')),
            ],
        };

        IndexerRunResult result = await CreateProcessor(source, store).ProcessOnceAsync(
            CreateConfiguration(startBlock: 100),
            CancellationToken.None);

        Assert.Equal(IndexerRunStatus.Reorganized, result.Status);
        Assert.Equal(104, store.RewindBlock);
        Assert.Equal(104, store.Checkpoint?.NextBlockNumber);
        Assert.Null(source.LastFromBlock);
    }

    [Fact]
    public async Task ChainChangeDuringRangeReadDoesNotAdvanceCheckpoint()
    {
        RawEvmLog log = Erc20TransferLogDecoderTests.CreateTransferLog();
        var source = new FakeLogSource
        {
            Head = 120,
            Logs = [log],
        };
        source.BlockHashes[log.BlockNumber] = Hash('c');
        var store = new FakeStateStore();

        IndexerRunResult result = await CreateProcessor(source, store).ProcessOnceAsync(
            CreateConfiguration(startBlock: 100, initialRange: 5),
            CancellationToken.None);

        Assert.Equal(IndexerRunStatus.ChainChangedDuringRead, result.Status);
        Assert.Null(store.Checkpoint);
    }

    [Fact]
    public async Task ReorganizationBeyondRetainedWindowFailsSafe()
    {
        var source = new FakeLogSource { Head = 120, Logs = [] };
        source.BlockHashes[104] = Hash('c');
        source.BlockHashes[103] = Hash('d');
        var store = new FakeStateStore
        {
            Checkpoint = new IndexerCheckpoint(1, 105, 10, Now),
            CanonicalBlocks =
            [
                new CanonicalBlock(1, 104, Hash('a')),
                new CanonicalBlock(1, 103, Hash('b')),
            ],
        };

        await Assert.ThrowsAsync<ReorganizationWindowExceededException>(
            () => CreateProcessor(source, store).ProcessOnceAsync(
                CreateConfiguration(startBlock: 100),
                CancellationToken.None));

        Assert.Null(store.RewindBlock);
        Assert.Equal(105, store.Checkpoint?.NextBlockNumber);
    }

    private static EvmIndexerProcessor CreateProcessor(
        IEvmLogSource source,
        IIndexerStateStore store) =>
        new(
            source,
            new FakeTokenMetadataProvider(),
            store,
            new FixedTimeProvider(Now));

    private static EvmNetworkConfiguration CreateConfiguration(
        long startBlock = 0,
        int confirmationDepth = 12,
        int initialRange = 100,
        int minimumRange = 10) =>
        new(
            "ethereum",
            1,
            startBlock,
            confirmationDepth,
            initialRange,
            minimumRange,
            1000,
            64);

    private static string Hash(char value) => $"0x{new string(value, 64)}";

    private sealed class FakeLogSource : IEvmLogSource
    {
        public long Head { get; set; }

        public IReadOnlyList<RawEvmLog> Logs { get; set; } = [];

        public bool ThrowRangeLimit { get; set; }

        public long? LastFromBlock { get; private set; }

        public long? LastToBlock { get; private set; }

        public Dictionary<long, string> BlockHashes { get; } = [];

        public Task<long> GetHeadBlockNumberAsync(
            long chainId,
            CancellationToken cancellationToken) => Task.FromResult(Head);

        public Task<string> GetBlockHashAsync(
            long chainId,
            long blockNumber,
            CancellationToken cancellationToken)
        {
            string? logHash = Logs
                .Where(log => log.BlockNumber == blockNumber)
                .Select(log => log.BlockHash)
                .FirstOrDefault();
            return Task.FromResult(
                BlockHashes.GetValueOrDefault(blockNumber) ??
                logHash ??
                $"0x{blockNumber:x64}");
        }

        public Task<IReadOnlyList<RawEvmLog>> GetTransferLogsAsync(
            long chainId,
            long fromBlock,
            long toBlock,
            CancellationToken cancellationToken)
        {
            LastFromBlock = fromBlock;
            LastToBlock = toBlock;
            return ThrowRangeLimit
                ? Task.FromException<IReadOnlyList<RawEvmLog>>(
                    new EvmRpcRangeLimitException("Range too large."))
                : Task.FromResult(Logs);
        }
    }

    private sealed class FakeTokenMetadataProvider : ITokenMetadataProvider
    {
        public TokenMetadata Metadata { get; set; } =
            new("USDT", "Tether USD", 6);

        public Task<TokenMetadata> GetAsync(
            long chainId,
            EvmAddress tokenContract,
            CancellationToken cancellationToken) =>
            Task.FromResult(Metadata);
    }

    private sealed class FakeStateStore : IIndexerStateStore
    {
        public IndexerCheckpoint? Checkpoint { get; set; }

        public IndexedTransfer[] LastCommittedTransfers { get; private set; } = [];

        public IReadOnlyList<CanonicalBlock> CanonicalBlocks { get; set; } = [];

        public long? RewindBlock { get; private set; }

        public Task<IndexerCheckpoint?> GetCheckpointAsync(
            long chainId,
            CancellationToken cancellationToken) => Task.FromResult(Checkpoint);

        public Task UpdateRangeSizeAsync(
            IndexerCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            Checkpoint = checkpoint;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CanonicalBlock>> GetRecentCanonicalBlocksAsync(
            long chainId,
            CancellationToken cancellationToken) => Task.FromResult(CanonicalBlocks);

        public Task<ReorganizationResult> RewindAsync(
            IndexerCheckpoint checkpoint,
            long rewindBlockNumber,
            DateTimeOffset detectedAtUtc,
            CancellationToken cancellationToken)
        {
            Checkpoint = checkpoint;
            RewindBlock = rewindBlockNumber;
            return Task.FromResult(new ReorganizationResult(rewindBlockNumber, 2, 2));
        }

        public Task<int> CommitBatchAsync(
            IndexerCheckpoint checkpoint,
            IReadOnlyCollection<IndexedTransfer> transfers,
            IReadOnlyCollection<CanonicalBlock> canonicalBlocks,
            int reorganizationWindow,
            CancellationToken cancellationToken)
        {
            Checkpoint = checkpoint;
            LastCommittedTransfers = transfers.ToArray();
            CanonicalBlocks = canonicalBlocks.OrderByDescending(block => block.BlockNumber).ToArray();
            return Task.FromResult(transfers.Count);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
