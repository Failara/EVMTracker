using EVMTracker.Watchlist.Domain.Common;
using MediatR;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

public sealed record RemoveTrackedAddressCommand(Guid TrackedAddressId, long TelegramUserId)
    : IRequest<Result>;
