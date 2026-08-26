using System.Net;
using System.Text;
using System.Text.Json;
using EVMTracker.TelegramGateway.Application.Watchlists;
using EVMTracker.TelegramGateway.Infrastructure.Watchlists;

namespace EVMTracker.TelegramGateway.UnitTests;

public sealed class WatchlistApiClientTests
{
    private static readonly Guid CorrelationId = Guid.Parse(
        "01991ef1-45ef-7d20-83ef-53cd755f09a7");
    private static readonly Guid TrackedAddressId = Guid.Parse(
        "01991ef2-1234-7d20-83ef-53cd755f09a7");

    [Fact]
    public async Task AddSendsExpectedPayloadAndCorrelationId()
    {
        var handler = new DelegateHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://watchlist/api/watchlist/", request.RequestUri?.ToString());
            Assert.Equal(
                CorrelationId.ToString("D"),
                request.Headers.GetValues(WatchlistApiClient.CorrelationHeaderName).Single());
            using JsonDocument json = await JsonDocument.ParseAsync(
                await request.Content!.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            Assert.Equal(1, json.RootElement.GetProperty("chainIds")[0].GetInt64());
            Assert.Equal(42, json.RootElement.GetProperty("telegramUserId").GetInt64());

            return JsonResponse(HttpStatusCode.Created, CreateAddressJson([1]));
        });
        WatchlistApiClient client = CreateClient(handler);

        WatchlistClientResult<TrackedAddressView> result = await client.AddAsync(
            new AddTrackedAddressRequest(
                "0x52908400098527886e0f7030069857d2e4169ee7",
                42,
                84,
                1,
                "Treasury"),
            CorrelationId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TrackedAddressId, result.Value.Id);
    }

    [Fact]
    public async Task ConflictProblemMapsToDuplicate()
    {
        var handler = new DelegateHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse(
                HttpStatusCode.Conflict,
                """
                {"title":"TrackedAddress.AlreadyTracked","detail":"Already tracked.","code":"TrackedAddress.AlreadyTracked"}
                """)));
        WatchlistApiClient client = CreateClient(handler);

        WatchlistClientResult<TrackedAddressView> result = await client.AddAsync(
            new AddTrackedAddressRequest("0x1", 42, 84, 1, null),
            CorrelationId,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WatchlistClientError.Duplicate, result.Failure?.Error);
    }

    [Fact]
    public async Task ListScopesRequestByTelegramUser()
    {
        var handler = new DelegateHandler((request, cancellationToken) =>
        {
            Assert.Equal("http://watchlist/api/watchlist/users/42", request.RequestUri?.ToString());
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, $"[{CreateAddressJson([1])}]"));
        });
        WatchlistApiClient client = CreateClient(handler);

        WatchlistClientResult<IReadOnlyList<TrackedAddressView>> result = await client.ListAsync(
            42,
            CorrelationId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(42, result.Value[0].TelegramUserId);
    }

    [Fact]
    public async Task RemovingFinalNetworkDeletesAggregate()
    {
        var requests = new Queue<
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>();
        requests.Enqueue((request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(
                JsonResponse(HttpStatusCode.OK, $"[{CreateAddressJson([1])}]"));
        });
        requests.Enqueue((request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal(
                $"http://watchlist/api/watchlist/{TrackedAddressId}?telegramUserId=42",
                request.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        WatchlistApiClient client = CreateClient(new QueueHandler(requests));

        WatchlistClientResult<bool> result = await client.RemoveNetworkAsync(
            new RemoveTrackedAddressNetworkRequest(
                "0x52908400098527886e0f7030069857d2e4169ee7",
                42,
                1),
            CorrelationId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task RemovingOneOfSeveralNetworksUpdatesAggregate()
    {
        var requests = new Queue<
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>();
        requests.Enqueue((request, cancellationToken) =>
            Task.FromResult(
                JsonResponse(HttpStatusCode.OK, $"[{CreateAddressJson([1, 137])}]")));
        requests.Enqueue(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            string content = await request.Content!
                .ReadAsStringAsync(cancellationToken);
            using JsonDocument json = JsonDocument.Parse(content);
            JsonElement chainIds = json.RootElement.GetProperty("chainIds");
            Assert.Equal(1, chainIds.GetArrayLength());
            Assert.Equal(137, chainIds[0].GetInt64());
            return JsonResponse(HttpStatusCode.OK, CreateAddressJson([137]));
        });
        WatchlistApiClient client = CreateClient(new QueueHandler(requests));

        WatchlistClientResult<bool> result = await client.RemoveNetworkAsync(
            new RemoveTrackedAddressNetworkRequest(
                "0x52908400098527886e0f7030069857d2e4169ee7",
                42,
                1),
            CorrelationId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task MissingAddressDuringRemoveMapsToNotFound()
    {
        var handler = new DelegateHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        WatchlistApiClient client = CreateClient(handler);

        WatchlistClientResult<bool> result = await client.RemoveNetworkAsync(
            new RemoveTrackedAddressNetworkRequest("0x1", 42, 1),
            CorrelationId,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WatchlistClientError.NotFound, result.Failure?.Error);
    }

    [Fact]
    public async Task NetworkFailureMapsToUnavailable()
    {
        var handler = new DelegateHandler((request, cancellationToken) =>
            throw new HttpRequestException("Connection refused."));
        WatchlistApiClient client = CreateClient(handler);

        WatchlistClientResult<IReadOnlyList<TrackedAddressView>> result = await client.ListAsync(
            42,
            CorrelationId,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(WatchlistClientError.Unavailable, result.Failure?.Error);
    }

    private static WatchlistApiClient CreateClient(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://watchlist/"),
                Timeout = TimeSpan.FromSeconds(2),
            });

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static string CreateAddressJson(long[] chainIds) => $$"""
        {
          "id":"{{TrackedAddressId}}",
          "address":"0x52908400098527886E0F7030069857D2E4169EE7",
          "telegramUserId":42,
          "telegramChatId":84,
          "label":"Treasury",
          "chainIds":{{JsonSerializer.Serialize(chainIds)}},
          "isActive":true,
          "createdAtUtc":"2026-08-26T12:00:00+00:00"
        }
        """;

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }

    private sealed class QueueHandler(
        Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> handlers)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handlers.Dequeue()(request, cancellationToken);
    }
}
