using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EVMTracker.TelegramGateway.Application.Abstractions;
using EVMTracker.TelegramGateway.Application.Watchlists;

namespace EVMTracker.TelegramGateway.Infrastructure.Watchlists;

public sealed class WatchlistApiClient(HttpClient httpClient) : IWatchlistClient
{
    public const string CorrelationHeaderName = "X-Correlation-ID";

    public async Task<WatchlistClientResult<TrackedAddressView>> AddAsync(
        AddTrackedAddressRequest request,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var payload = new AddTrackedAddressApiRequest(
            request.Address,
            request.TelegramUserId,
            request.TelegramChatId,
            [request.ChainId],
            request.Label);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/watchlist/")
        {
            Content = JsonContent.Create(payload),
        };
        AddCorrelationId(httpRequest, correlationId);

        return await SendForValueAsync<TrackedAddressView>(
            httpRequest,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<WatchlistClientResult<IReadOnlyList<TrackedAddressView>>> ListAsync(
        long telegramUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/watchlist/users/{telegramUserId}");
        AddCorrelationId(request, correlationId);

        WatchlistClientResult<TrackedAddressView[]> result =
            await SendForValueAsync<TrackedAddressView[]>(
                request,
                cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? WatchlistClientResult.Success<IReadOnlyList<TrackedAddressView>>(result.Value)
            : WatchlistClientResult.Failed<IReadOnlyList<TrackedAddressView>>(
                result.Failure!.Error,
                result.Failure.Detail);
    }

    public async Task<WatchlistClientResult<bool>> RemoveNetworkAsync(
        RemoveTrackedAddressNetworkRequest request,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        WatchlistClientResult<IReadOnlyList<TrackedAddressView>> listResult =
            await ListAsync(
                request.TelegramUserId,
                correlationId,
                cancellationToken).ConfigureAwait(false);
        if (!listResult.IsSuccess)
        {
            return WatchlistClientResult.Failed<bool>(
                listResult.Failure!.Error,
                listResult.Failure.Detail);
        }

        TrackedAddressView? trackedAddress = listResult.Value.FirstOrDefault(
            item => item.IsActive &&
                string.Equals(item.Address, request.Address, StringComparison.OrdinalIgnoreCase) &&
                item.ChainIds.Contains(request.ChainId));
        if (trackedAddress is null)
        {
            return WatchlistClientResult.Failed<bool>(
                WatchlistClientError.NotFound,
                "The tracked address was not found for the requested network.");
        }

        return trackedAddress.ChainIds.Count == 1
            ? await DeleteAsync(
                trackedAddress,
                request.TelegramUserId,
                correlationId,
                cancellationToken).ConfigureAwait(false)
            : await DisableNetworkAsync(
                trackedAddress,
                request.TelegramUserId,
                request.ChainId,
                correlationId,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<WatchlistClientResult<WatchlistServiceStatus>> GetStatusAsync(
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "health/live");
        AddCorrelationId(request, correlationId);

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? WatchlistClientResult.Success(
                    new WatchlistServiceStatus(true))
                : WatchlistClientResult.Failed<WatchlistServiceStatus>(
                    WatchlistClientError.Unavailable,
                    "The Watchlist health endpoint returned an unsuccessful response.");
        }
        catch (HttpRequestException exception)
        {
            return WatchlistClientResult.Failed<WatchlistServiceStatus>(
                WatchlistClientError.Unavailable,
                exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return WatchlistClientResult.Failed<WatchlistServiceStatus>(
                WatchlistClientError.Unavailable,
                exception.Message);
        }
    }

    private async Task<WatchlistClientResult<bool>> DeleteAsync(
        TrackedAddressView trackedAddress,
        long telegramUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"api/watchlist/{trackedAddress.Id}?telegramUserId={telegramUserId}");
        AddCorrelationId(request, correlationId);

        return await SendWithoutValueAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WatchlistClientResult<bool>> DisableNetworkAsync(
        TrackedAddressView trackedAddress,
        long telegramUserId,
        long chainId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        long[] remainingChainIds = trackedAddress.ChainIds
            .Where(existingChainId => existingChainId != chainId)
            .ToArray();
        var payload = new UpdateTrackedAddressApiRequest(
            telegramUserId,
            remainingChainIds,
            trackedAddress.Label);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"api/watchlist/{trackedAddress.Id}")
        {
            Content = JsonContent.Create(payload),
        };
        AddCorrelationId(request, correlationId);

        WatchlistClientResult<TrackedAddressView> result =
            await SendForValueAsync<TrackedAddressView>(
                request,
                cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? WatchlistClientResult.Success(true)
            : WatchlistClientResult.Failed<bool>(
                result.Failure!.Error,
                result.Failure.Detail);
    }

    private async Task<WatchlistClientResult<bool>> SendWithoutValueAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? WatchlistClientResult.Success(true)
                : await CreateFailureAsync<bool>(response, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return Unavailable<bool>(exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable<bool>(exception.Message);
        }
    }

    private async Task<WatchlistClientResult<T>> SendForValueAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return await CreateFailureAsync<T>(response, cancellationToken).ConfigureAwait(false);
            }

            T? value = await response.Content.ReadFromJsonAsync<T>(
                cancellationToken).ConfigureAwait(false);
            return value is null
                ? WatchlistClientResult.Failed<T>(
                    WatchlistClientError.Unexpected,
                    "The Watchlist service returned an empty response.")
                : WatchlistClientResult.Success(value);
        }
        catch (HttpRequestException exception)
        {
            return Unavailable<T>(exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable<T>(exception.Message);
        }
        catch (JsonException exception)
        {
            return WatchlistClientResult.Failed<T>(
                WatchlistClientError.Unexpected,
                exception.Message);
        }
    }

    private static async Task<WatchlistClientResult<T>> CreateFailureAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string detail = response.ReasonPhrase ?? "Watchlist request failed.";
        string? code = null;

        try
        {
            using JsonDocument problem = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            JsonElement root = problem.RootElement;
            if (root.TryGetProperty("detail", out JsonElement detailElement))
            {
                detail = detailElement.GetString() ?? detail;
            }

            if (root.TryGetProperty("code", out JsonElement codeElement))
            {
                code = codeElement.GetString();
            }
        }
        catch (JsonException)
        {
            // The HTTP status still provides a stable fallback when an upstream body is malformed.
        }

        WatchlistClientError error = code switch
        {
            "TrackedAddress.AlreadyTracked" => WatchlistClientError.Duplicate,
            "TrackedAddress.NotFound" => WatchlistClientError.NotFound,
            "TrackedAddress.Unauthorized" => WatchlistClientError.Unauthorized,
            _ => response.StatusCode switch
            {
                HttpStatusCode.Conflict => WatchlistClientError.Duplicate,
                HttpStatusCode.NotFound => WatchlistClientError.NotFound,
                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden => WatchlistClientError.Unauthorized,
                HttpStatusCode.BadGateway or
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout => WatchlistClientError.Unavailable,
                _ when (int)response.StatusCode >= 500 => WatchlistClientError.Unavailable,
                _ => WatchlistClientError.Validation,
            },
        };

        return WatchlistClientResult.Failed<T>(error, detail);
    }

    private static WatchlistClientResult<T> Unavailable<T>(string detail) =>
        WatchlistClientResult.Failed<T>(WatchlistClientError.Unavailable, detail);

    private static void AddCorrelationId(HttpRequestMessage request, Guid correlationId) =>
        request.Headers.TryAddWithoutValidation(
            CorrelationHeaderName,
            correlationId.ToString("D"));

    private sealed record AddTrackedAddressApiRequest(
        string Address,
        long TelegramUserId,
        long TelegramChatId,
        IReadOnlyCollection<long> ChainIds,
        string? Label);

    private sealed record UpdateTrackedAddressApiRequest(
        long TelegramUserId,
        IReadOnlyCollection<long> ChainIds,
        string? Label);
}
