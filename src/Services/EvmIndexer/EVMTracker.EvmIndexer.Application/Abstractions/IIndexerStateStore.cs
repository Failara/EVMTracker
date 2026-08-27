using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Indexing;

namespace EVMTracker.EvmIndexer.Application.Abstractions;

public interface IIndexerStateStore
{
    Task<IndexerCheckpoint?> GetCheckpointAsync(
        long chainId,
        CancellationToken cancellationToken);

    Task UpdateRangeSizeAsync(
        IndexerCheckpoint checkpoint,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CanonicalBlock>> GetRecentCanonicalBlocksAsync(
        long chainId,
        CancellationToken cancellationToken);

    Task<ReorganizationResult> RewindAsync(
        IndexerCheckpoint checkpoint,
        long rewindBlockNumber,
        DateTimeOffset detectedAtUtc,
        CancellationToken cancellationToken);

    Task<int> CommitBatchAsync(
        IndexerCheckpoint checkpoint,
        IReadOnlyCollection<IndexedTransfer> transfers,
        IReadOnlyCollection<CanonicalBlock> canonicalBlocks,
        int reorganizationWindow,
        CancellationToken cancellationToken);
}
