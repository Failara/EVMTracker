using EVMTracker.Watchlist.Domain.Common;
using MediatR;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

public sealed record UpdateTrackedAddressCommand(
    Guid TrackedAddressId,
    long TelegramUserId,
    IReadOnlyCollection<long> ChainIds,
    string? Label) : IRequest<Result<TrackedAddressDto>>;
