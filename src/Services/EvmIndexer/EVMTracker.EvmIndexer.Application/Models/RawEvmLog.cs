namespace EVMTracker.EvmIndexer.Application.Models;

public sealed record RawEvmLog(
    string ContractAddress,
    IReadOnlyList<string> Topics,
    string Data,
    string TransactionHash,
    long BlockNumber,
    string BlockHash,
    long LogIndex,
    bool IsRemoved);
