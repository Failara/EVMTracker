using EVMTracker.Watchlist.Api.Contracts;
using EVMTracker.Watchlist.Application.Abstractions.Messaging;
using EVMTracker.Watchlist.Application.TrackedAddresses;
using EVMTracker.Watchlist.Domain.Common;
using MediatR;

namespace EVMTracker.Watchlist.Api.Endpoints;

public static class WatchlistEndpoints
{
    public static IEndpointRouteBuilder MapWatchlistEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/watchlist")
            .WithTags("Watchlist");

        group.MapPost("/", AddTrackedAddressAsync);
        group.MapGet("/users/{telegramUserId:long}", ListTrackedAddressesAsync);
        group.MapPut("/{trackedAddressId:guid}", UpdateTrackedAddressAsync);
        group.MapDelete("/{trackedAddressId:guid}", RemoveTrackedAddressAsync);
        group.MapPost("/events/replay", ReplayEventsAsync);

        return endpoints;
    }

    private static async Task<IResult> AddTrackedAddressAsync(
        AddTrackedAddressRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<TrackedAddressDto> result = await sender.Send(
            new AddTrackedAddressCommand(
                request.Address,
                request.TelegramUserId,
                request.TelegramChatId,
                request.ChainIds,
                request.Label),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Created($"/api/watchlist/{result.Value.Id}", result.Value)
            : WatchlistEndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ListTrackedAddressesAsync(
        long telegramUserId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<TrackedAddressDto>> result = await sender.Send(
            new ListTrackedAddressesQuery(telegramUserId),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : WatchlistEndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateTrackedAddressAsync(
        Guid trackedAddressId,
        UpdateTrackedAddressRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<TrackedAddressDto> result = await sender.Send(
            new UpdateTrackedAddressCommand(
                trackedAddressId,
                request.TelegramUserId,
                request.ChainIds,
                request.Label),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : WatchlistEndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RemoveTrackedAddressAsync(
        Guid trackedAddressId,
        long telegramUserId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(
            new RemoveTrackedAddressCommand(trackedAddressId, telegramUserId),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.NoContent()
            : WatchlistEndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ReplayEventsAsync(
        IWatchlistEventReplayService replayService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        Guid correlationId = TryGetCorrelationId(httpContext) ?? Guid.CreateVersion7();
        int replayedEvents = await replayService.ReplayAsync(
            correlationId,
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { CorrelationId = correlationId, ReplayedEvents = replayedEvents });
    }

    private static Guid? TryGetCorrelationId(HttpContext httpContext) =>
        Guid.TryParse(
            httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault(),
            out Guid correlationId)
            ? correlationId
            : null;
}
