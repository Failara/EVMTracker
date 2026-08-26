namespace EVMTracker.TelegramGateway.Application.Watchlists;

public sealed record WatchlistClientFailure(WatchlistClientError Error, string Detail);
