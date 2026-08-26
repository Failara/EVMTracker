using EVMTracker.Contracts.Watchlists;

namespace EVMTracker.Watchlist.Application.Abstractions.Messaging;

public interface IOutboxWriter
{
    void Add(IWatchlistIntegrationEvent integrationEvent);
}
