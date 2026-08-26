namespace EVMTracker.TelegramGateway.Application.Watchlists;

public enum WatchlistClientError
{
    Validation,
    Duplicate,
    NotFound,
    Unauthorized,
    Unavailable,
    Unexpected,
}
