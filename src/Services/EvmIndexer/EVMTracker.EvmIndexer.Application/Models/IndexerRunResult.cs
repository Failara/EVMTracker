namespace EVMTracker.EvmIndexer.Application.Models;

public enum IndexerRunStatus
{
    NoConfirmedBlocks,
    RangeReduced,
    Processed,
    Reorganized,
    ChainChangedDuringRead,
}

public sealed record IndexerRunResult(
    IndexerRunStatus Status,
    long? FromBlock,
    long? ToBlock,
    int RetrievedLogCount,
    int PersistedTransferCount,
    int MalformedLogCount,
    int NextRangeSize,
    int OrphanedTransferCount = 0,
    long ChainHead = 0,
    long LastProcessedBlock = -1);
