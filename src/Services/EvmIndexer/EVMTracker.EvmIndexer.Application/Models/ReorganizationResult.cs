namespace EVMTracker.EvmIndexer.Application.Models;

public sealed record ReorganizationResult(
    long RewindBlockNumber,
    int OrphanedTransferCount,
    int PublishedOrphanEventCount);
