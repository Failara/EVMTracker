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

    Task<int> CommitBatchAsync(
        IndexerCheckpoint checkpoint,
        IReadOnlyCollection<IndexedTransfer> transfers,
        CancellationToken cancellationToken);
}
