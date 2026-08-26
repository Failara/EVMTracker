using EVMTracker.Watchlist.Application.Abstractions.Messaging;
using EVMTracker.Watchlist.Application.Abstractions.Persistence;
using EVMTracker.Watchlist.Domain.Common;
using EVMTracker.Watchlist.Domain.TrackedAddresses;
using EVMTracker.Watchlist.Domain.ValueObjects;
using MediatR;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

internal sealed class AddTrackedAddressCommandHandler(
    ITrackedAddressRepository repository,
    IOutboxWriter outboxWriter,
    IWatchlistUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<AddTrackedAddressCommand, Result<TrackedAddressDto>>
{
    public async Task<Result<TrackedAddressDto>> Handle(
        AddTrackedAddressCommand request,
        CancellationToken cancellationToken)
    {
        Result<Address> addressResult = Address.Create(request.RawAddress);
        if (addressResult.IsFailure)
        {
            return Result.Failure<TrackedAddressDto>(addressResult.Error);
        }

        Result<IReadOnlyCollection<ChainId>> chainIdsResult =
            ChainIdCollection.Create(request.ChainIds);
        if (chainIdsResult.IsFailure)
        {
            return Result.Failure<TrackedAddressDto>(chainIdsResult.Error);
        }

        Result<TrackedAddress> trackedAddressResult = TrackedAddress.Create(
            addressResult.Value,
            request.TelegramUserId,
            chainIdsResult.Value,
            request.Label,
            request.TelegramChatId);
        if (trackedAddressResult.IsFailure)
        {
            return Result.Failure<TrackedAddressDto>(trackedAddressResult.Error);
        }

        IReadOnlyList<TrackedAddress> existingTrackedAddresses =
            await repository.ListByUserAndAddressAsync(
                request.TelegramUserId,
                addressResult.Value,
                cancellationToken).ConfigureAwait(false);

        foreach (ChainId chainId in chainIdsResult.Value)
        {
            Result policyResult = TrackedAddressPolicy.EnsureCanTrack(
                request.TelegramUserId,
                addressResult.Value,
                chainId,
                existingTrackedAddresses);
            if (policyResult.IsFailure)
            {
                return Result.Failure<TrackedAddressDto>(policyResult.Error);
            }
        }

        TrackedAddress trackedAddress = trackedAddressResult.Value;
        repository.Add(trackedAddress);
        outboxWriter.Add(
            WatchlistEventFactory.CreateAdded(trackedAddress, timeProvider.GetUtcNow()));

        WatchlistCommitResult commitResult =
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return commitResult == WatchlistCommitResult.DuplicateTrackedAddress
            ? Result.Failure<TrackedAddressDto>(TrackedAddressErrors.AlreadyTracked)
            : Result.Success(TrackedAddressDto.FromDomain(trackedAddress));
    }
}
