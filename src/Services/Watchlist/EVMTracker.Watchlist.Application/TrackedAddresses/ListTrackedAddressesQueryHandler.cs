using EVMTracker.Watchlist.Application.Abstractions.Persistence;
using EVMTracker.Watchlist.Domain.Common;
using EVMTracker.Watchlist.Domain.TrackedAddresses;
using MediatR;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

internal sealed class ListTrackedAddressesQueryHandler(ITrackedAddressRepository repository)
    : IRequestHandler<ListTrackedAddressesQuery, Result<IReadOnlyList<TrackedAddressDto>>>
{
    public async Task<Result<IReadOnlyList<TrackedAddressDto>>> Handle(
        ListTrackedAddressesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.TelegramUserId <= 0)
        {
            return Result.Failure<IReadOnlyList<TrackedAddressDto>>(
                TrackedAddressErrors.InvalidTelegramUserId);
        }

        IReadOnlyList<TrackedAddress> trackedAddresses =
            await repository.ListByUserAsync(
                request.TelegramUserId,
                cancellationToken).ConfigureAwait(false);
        TrackedAddressDto[] response = trackedAddresses
            .Select(TrackedAddressDto.FromDomain)
            .ToArray();

        return Result.Success<IReadOnlyList<TrackedAddressDto>>(response);
    }
}
