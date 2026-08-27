namespace EVMTracker.EvmIndexer.Application.Indexing;

public sealed class ReorganizationWindowExceededException(string message)
    : Exception(message);
