namespace EVMTracker.EvmIndexer.Application.Models;

public sealed record EvmNetworkConfiguration(
    string Name,
    long ChainId,
    long StartBlock,
    int ConfirmationDepth,
    int InitialBlockRange,
    int MinimumBlockRange,
    int MaximumBlockRange,
    int ReorganizationWindow = 64);
