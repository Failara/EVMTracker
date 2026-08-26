using EVMTracker.Watchlist.Domain.Common;

namespace EVMTracker.Watchlist.Api.Endpoints;

internal static class WatchlistEndpointResults
{
    public static IResult Problem(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        int statusCode = error.Code switch
        {
            "TrackedAddress.NotFound" => StatusCodes.Status404NotFound,
            "TrackedAddress.Unauthorized" => StatusCodes.Status403Forbidden,
            "TrackedAddress.AlreadyTracked" => StatusCodes.Status409Conflict,
            "TrackedAddress.AlreadyRemoved" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Description,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
            });
    }
}
