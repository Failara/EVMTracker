using EVMTracker.TelegramGateway.Application.Watchlists;

namespace EVMTracker.TelegramGateway.Application.Abstractions;

public interface IWatchlistClient
{
    Task<WatchlistClientResult<TrackedAddressView>> AddAsync(
        AddTrackedAddressRequest request,
        Guid correlationId,
        CancellationToken cancellationToken);

    Task<WatchlistClientResult<IReadOnlyList<TrackedAddressView>>> ListAsync(
        long telegramUserId,
        Guid correlationId,
        CancellationToken cancellationToken);

    Task<WatchlistClientResult<bool>> RemoveNetworkAsync(
        RemoveTrackedAddressNetworkRequest request,
        Guid correlationId,
        CancellationToken cancellationToken);

    Task<WatchlistClientResult<WatchlistServiceStatus>> GetStatusAsync(
        Guid correlationId,
        CancellationToken cancellationToken);
}
