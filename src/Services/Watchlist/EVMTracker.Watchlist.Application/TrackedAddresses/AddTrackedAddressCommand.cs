using EVMTracker.Watchlist.Domain.Common;
using MediatR;

namespace EVMTracker.Watchlist.Application.TrackedAddresses;

public sealed record AddTrackedAddressCommand(
    string RawAddress,
    long TelegramUserId,
    long TelegramChatId,
    IReadOnlyCollection<long> ChainIds,
    string? Label) : IRequest<Result<TrackedAddressDto>>;
