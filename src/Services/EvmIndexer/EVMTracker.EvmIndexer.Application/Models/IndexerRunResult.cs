namespace EVMTracker.EvmIndexer.Application.Models;

public enum IndexerRunStatus
{
    NoConfirmedBlocks,
    RangeReduced,
    Processed,
}

public sealed record IndexerRunResult(
    IndexerRunStatus Status,
    long? FromBlock,
    long? ToBlock,
    int RetrievedLogCount,
    int PersistedTransferCount,
    int MalformedLogCount,
    int NextRangeSize);
