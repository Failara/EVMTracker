using EVMTracker.Watchlist.Application.Abstractions.Messaging;
using EVMTracker.Watchlist.Application.Abstractions.Persistence;
using EVMTracker.Watchlist.Domain.Common;
using EVMTracker.Watchlist.Domain.TrackedAddresses;
using MediatR;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

internal sealed class RemoveTrackedAddressCommandHandler(
    ITrackedAddressRepository repository,
    IOutboxWriter outboxWriter,
    IWatchlistUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<RemoveTrackedAddressCommand, Result>
{
    public async Task<Result> Handle(
        RemoveTrackedAddressCommand request,
        CancellationToken cancellationToken)
    {
        TrackedAddress? trackedAddress = await repository.GetByIdAsync(
            request.TrackedAddressId,
            cancellationToken).ConfigureAwait(false);
        if (trackedAddress is null)
        {
            return Result.Failure(WatchlistApplicationErrors.NotFound);
        }

        Result removeResult = trackedAddress.Remove(request.TelegramUserId);
        if (removeResult.IsFailure)
        {
            return removeResult;
        }

        await repository.UpdateAsync(trackedAddress, cancellationToken).ConfigureAwait(false);
        outboxWriter.Add(
            WatchlistEventFactory.CreateRemoved(trackedAddress, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
