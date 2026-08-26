using EVMTracker.TelegramGateway.Application.Abstractions;
using EVMTracker.TelegramGateway.Application.Commands;
using EVMTracker.TelegramGateway.Application.Watchlists;
using EVMTracker.TelegramGateway.Infrastructure.Chains;

namespace EVMTracker.TelegramGateway.UnitTests;

public sealed class TelegramCommandServiceTests
{
    private static readonly Guid CorrelationId = Guid.Parse(
        "01991ef1-45ef-7d20-83ef-53cd755f09a7");

    [Fact]
    public async Task AddConvertsCommandToWatchlistRequest()
    {
        var client = new FakeWatchlistClient
        {
            AddResult = WatchlistClientResult.Success(
                CreateAddress("0x52908400098527886E0F7030069857D2E4169EE7", "Treasury")),
        };
        TelegramCommandService service = CreateService(client);

        string response = await service.HandleAsync(
            CreateRequest(
                "/add ethereum 0x52908400098527886e0f7030069857d2e4169ee7 Treasury"),
            CancellationToken.None);

        Assert.NotNull(client.LastAddRequest);
        Assert.Equal(42, client.LastAddRequest.TelegramUserId);
        Assert.Equal(84, client.LastAddRequest.TelegramChatId);
        Assert.Equal(1, client.LastAddRequest.ChainId);
        Assert.Equal("Treasury", client.LastAddRequest.Label);
        Assert.Equal(CorrelationId, client.LastCorrelationId);
        Assert.Contains("Now tracking Treasury", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListUsesCurrentTelegramUserAndRendersOnlyReturnedEntries()
    {
        var client = new FakeWatchlistClient
        {
            ListResult = WatchlistClientResult.Success<IReadOnlyList<TrackedAddressView>>(
                [CreateAddress("0x52908400098527886E0F7030069857D2E4169EE7", "Treasury")]),
        };
        TelegramCommandService service = CreateService(client);

        string response = await service.HandleAsync(
            CreateRequest("/list"),
            CancellationToken.None);

        Assert.Equal(42, client.LastListedUserId);
        Assert.Contains("Treasury", response, StringComparison.Ordinal);
        Assert.Contains("Ethereum", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicateApplicationErrorMapsToFriendlyText()
    {
        var client = new FakeWatchlistClient
        {
            AddResult = WatchlistClientResult.Failed<TrackedAddressView>(
                WatchlistClientError.Duplicate,
                "Already tracked."),
        };
        TelegramCommandService service = CreateService(client);

        string response = await service.HandleAsync(
            CreateRequest(
                "/add ethereum 0x52908400098527886e0f7030069857d2e4169ee7"),
            CancellationToken.None);

        Assert.Equal("That address is already tracked on this network.", response);
    }

    [Fact]
    public async Task UnavailableServiceMapsToRetryGuidance()
    {
        var client = new FakeWatchlistClient
        {
            ListResult = WatchlistClientResult.Failed<IReadOnlyList<TrackedAddressView>>(
                WatchlistClientError.Unavailable,
                "Connection refused."),
        };
        TelegramCommandService service = CreateService(client);

        string response = await service.HandleAsync(
            CreateRequest("/list"),
            CancellationToken.None);

        Assert.Contains("temporarily unavailable", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsupportedNetworkDoesNotCallWatchlist()
    {
        var client = new FakeWatchlistClient();
        TelegramCommandService service = CreateService(client);

        string response = await service.HandleAsync(
            CreateRequest(
                "/add unknown 0x52908400098527886e0f7030069857d2e4169ee7"),
            CancellationToken.None);

        Assert.Null(client.LastAddRequest);
        Assert.Contains("Unsupported network 'unknown'", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusReportsWatchlistAvailability()
    {
        var client = new FakeWatchlistClient
        {
            StatusResult = WatchlistClientResult.Success(new WatchlistServiceStatus(true)),
        };
        TelegramCommandService service = CreateService(client);

        string response = await service.HandleAsync(
            CreateRequest("/status"),
            CancellationToken.None);

        Assert.Contains("is available", response, StringComparison.Ordinal);
    }

    private static TelegramCommandService CreateService(IWatchlistClient client) =>
        new(
            new ConfiguredChainCatalog(
                [
                    new ChainNetwork("ethereum", "Ethereum", 1),
                    new ChainNetwork("sepolia", "Sepolia", 11155111),
                ]),
            client);

    private static TelegramCommandRequest CreateRequest(string text) =>
        new(42, 84, text, CorrelationId);

    private static TrackedAddressView CreateAddress(string address, string? label) =>
        new(
            Guid.Parse("01991ef2-1234-7d20-83ef-53cd755f09a7"),
            address,
            42,
            84,
            label,
            [1],
            true,
            new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));

    private sealed class FakeWatchlistClient : IWatchlistClient
    {
        public WatchlistClientResult<TrackedAddressView> AddResult { get; set; } =
            WatchlistClientResult.Failed<TrackedAddressView>(
                WatchlistClientError.Unexpected,
                "Not configured.");

        public WatchlistClientResult<IReadOnlyList<TrackedAddressView>> ListResult { get; set; } =
            WatchlistClientResult.Success<IReadOnlyList<TrackedAddressView>>([]);

        public WatchlistClientResult<bool> RemoveResult { get; set; } =
            WatchlistClientResult.Success(true);

        public WatchlistClientResult<WatchlistServiceStatus> StatusResult { get; set; } =
            WatchlistClientResult.Success(new WatchlistServiceStatus(true));

        public AddTrackedAddressRequest? LastAddRequest { get; private set; }

        public long? LastListedUserId { get; private set; }

        public Guid? LastCorrelationId { get; private set; }

        public Task<WatchlistClientResult<TrackedAddressView>> AddAsync(
            AddTrackedAddressRequest request,
            Guid correlationId,
            CancellationToken cancellationToken)
        {
            LastAddRequest = request;
            LastCorrelationId = correlationId;
            return Task.FromResult(AddResult);
        }

        public Task<WatchlistClientResult<IReadOnlyList<TrackedAddressView>>> ListAsync(
            long telegramUserId,
            Guid correlationId,
            CancellationToken cancellationToken)
        {
            LastListedUserId = telegramUserId;
            LastCorrelationId = correlationId;
            return Task.FromResult(ListResult);
        }

        public Task<WatchlistClientResult<bool>> RemoveNetworkAsync(
            RemoveTrackedAddressNetworkRequest request,
            Guid correlationId,
            CancellationToken cancellationToken) => Task.FromResult(RemoveResult);

        public Task<WatchlistClientResult<WatchlistServiceStatus>> GetStatusAsync(
            Guid correlationId,
            CancellationToken cancellationToken) => Task.FromResult(StatusResult);
    }
}
