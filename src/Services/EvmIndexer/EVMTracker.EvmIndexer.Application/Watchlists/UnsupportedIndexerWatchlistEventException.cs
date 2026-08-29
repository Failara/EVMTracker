namespace EVMTracker.EvmIndexer.Application.Watchlists;

public sealed class UnsupportedIndexerWatchlistEventException(string message)
    : Exception(message);
