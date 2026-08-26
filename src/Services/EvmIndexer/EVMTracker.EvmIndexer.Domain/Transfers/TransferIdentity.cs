namespace EVMTracker.EvmIndexer.Domain.Transfers;

public sealed record TransferIdentity(long ChainId, Hash256 TransactionHash, long LogIndex);
