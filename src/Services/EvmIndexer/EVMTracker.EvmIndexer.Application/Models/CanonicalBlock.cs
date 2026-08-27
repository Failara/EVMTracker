namespace EVMTracker.EvmIndexer.Application.Models;

public sealed record CanonicalBlock(long ChainId, long BlockNumber, string BlockHash);
