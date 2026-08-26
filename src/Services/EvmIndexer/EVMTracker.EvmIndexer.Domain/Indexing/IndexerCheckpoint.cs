namespace EVMTracker.EvmIndexer.Domain.Indexing;

public sealed record IndexerCheckpoint(
    long ChainId,
    long NextBlockNumber,
    int CurrentRangeSize,
    DateTimeOffset UpdatedAtUtc);
