using EVMTracker.Watchlist.Application.Abstractions.Messaging;
using EVMTracker.Watchlist.Application.Abstractions.Persistence;
using EVMTracker.Watchlist.Domain.Common;
using EVMTracker.Watchlist.Domain.TrackedAddresses;
using EVMTracker.Watchlist.Domain.ValueObjects;
using MediatR;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

internal sealed class UpdateTrackedAddressCommandHandler(
    ITrackedAddressRepository repository,
    IOutboxWriter outboxWriter,
    IWatchlistUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateTrackedAddressCommand, Result<TrackedAddressDto>>
{
    public async Task<Result<TrackedAddressDto>> Handle(
        UpdateTrackedAddressCommand request,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyCollection<ChainId>> chainIdsResult =
            ChainIdCollection.Create(request.ChainIds);
        if (chainIdsResult.IsFailure)
        {
            return Result.Failure<TrackedAddressDto>(chainIdsResult.Error);
        }

        TrackedAddress? trackedAddress = await repository.GetByIdAsync(
            request.TrackedAddressId,
            cancellationToken).ConfigureAwait(false);
        if (trackedAddress is null)
        {
            return Result.Failure<TrackedAddressDto>(WatchlistApplicationErrors.NotFound);
        }

        Result renameResult = trackedAddress.Rename(request.TelegramUserId, request.Label);
        if (renameResult.IsFailure)
        {
            return Result.Failure<TrackedAddressDto>(renameResult.Error);
        }

        HashSet<ChainId> requestedNetworks = [.. chainIdsResult.Value];
        ChainId[] networksToEnable = requestedNetworks
            .Except(trackedAddress.EnabledNetworks)
            .ToArray();
        ChainId[] networksToDisable = trackedAddress.EnabledNetworks
            .Except(requestedNetworks)
            .ToArray();

        foreach (ChainId chainId in networksToEnable)
        {
            Result enableResult = trackedAddress.EnableNetwork(request.TelegramUserId, chainId);
            if (enableResult.IsFailure)
            {
                return Result.Failure<TrackedAddressDto>(enableResult.Error);
            }
        }

        foreach (ChainId chainId in networksToDisable)
        {
            Result disableResult = trackedAddress.DisableNetwork(request.TelegramUserId, chainId);
            if (disableResult.IsFailure)
            {
                return Result.Failure<TrackedAddressDto>(disableResult.Error);
            }
        }

        await repository.UpdateAsync(trackedAddress, cancellationToken).ConfigureAwait(false);
        outboxWriter.Add(
            WatchlistEventFactory.CreateUpdated(trackedAddress, timeProvider.GetUtcNow()));

        WatchlistCommitResult commitResult =
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return commitResult == WatchlistCommitResult.DuplicateTrackedAddress
            ? Result.Failure<TrackedAddressDto>(TrackedAddressErrors.AlreadyTracked)
            : Result.Success(TrackedAddressDto.FromDomain(trackedAddress));
    }
}
