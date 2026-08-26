using EVMTracker.Contracts.Watchlists;
using EVMTracker.Watchlist.Application.Abstractions.Messaging;
using EVMTracker.Watchlist.Application.Abstractions.Persistence;
using EVMTracker.Watchlist.Application.TrackedAddresses;
using EVMTracker.Watchlist.Domain.TrackedAddresses;
using EVMTracker.Watchlist.Domain.ValueObjects;

namespace EVMTracker.Watchlist.UnitTests;

public sealed class ApplicationHandlerTests
{
    private const long TelegramUserId = 1001;
    private const long TelegramChatId = -100200300;
    private const string RawAddress = "0x52908400098527886e0f7030069857d2e4169ee7";

    private static readonly DateTimeOffset FixedNow =
        new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddHandlerPersistsAggregateAndOutboxEventBeforeCommit()
    {
        var repository = new InMemoryTrackedAddressRepository();
        var outbox = new CapturingOutboxWriter();
        var unitOfWork = new StubUnitOfWork();
        var handler = new AddTrackedAddressCommandHandler(
            repository,
            outbox,
            unitOfWork,
            new FixedTimeProvider(FixedNow));

        var result = await handler.Handle(
            new AddTrackedAddressCommand(
                RawAddress,
                TelegramUserId,
                TelegramChatId,
                [1, 137],
                " Treasury "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("0x52908400098527886E0F7030069857D2E4169EE7", result.Value.Address);
        Assert.Equal("Treasury", result.Value.Label);
        Assert.Single(repository.Items);
        AddressAddedV1 addedEvent = Assert.IsType<AddressAddedV1>(Assert.Single(outbox.Events));
        Assert.Equal(result.Value.Id, addedEvent.TrackedAddressId);
        Assert.Equal(FixedNow, addedEvent.OccurredAtUtc);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task AddHandlerRejectsDuplicateWithoutWritingOutbox()
    {
        var repository = new InMemoryTrackedAddressRepository();
        repository.Add(CreateTrackedAddress(TelegramUserId, [1]));
        var outbox = new CapturingOutboxWriter();
        var unitOfWork = new StubUnitOfWork();
        var handler = new AddTrackedAddressCommandHandler(
            repository,
            outbox,
            unitOfWork,
            new FixedTimeProvider(FixedNow));

        var result = await handler.Handle(
            new AddTrackedAddressCommand(
                RawAddress,
                TelegramUserId,
                TelegramChatId,
                [1],
                null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TrackedAddressErrors.AlreadyTracked, result.Error);
        Assert.Empty(outbox.Events);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task AddHandlerMapsConcurrentDatabaseDuplicateToDomainFailure()
    {
        var repository = new InMemoryTrackedAddressRepository();
        var outbox = new CapturingOutboxWriter();
        var unitOfWork = new StubUnitOfWork
        {
            CommitResult = WatchlistCommitResult.DuplicateTrackedAddress,
        };
        var handler = new AddTrackedAddressCommandHandler(
            repository,
            outbox,
            unitOfWork,
            new FixedTimeProvider(FixedNow));

        var result = await handler.Handle(
            new AddTrackedAddressCommand(
                RawAddress,
                TelegramUserId,
                TelegramChatId,
                [1],
                null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TrackedAddressErrors.AlreadyTracked, result.Error);
    }

    [Fact]
    public async Task ListHandlerReturnsOnlyCurrentUsersActiveAddresses()
    {
        var repository = new InMemoryTrackedAddressRepository();
        repository.Add(CreateTrackedAddress(TelegramUserId, [1]));
        repository.Add(CreateTrackedAddress(2002, [137]));
        TrackedAddress removed = CreateTrackedAddress(TelegramUserId, [56]);
        removed.Remove(TelegramUserId);
        repository.Add(removed);
        var handler = new ListTrackedAddressesQueryHandler(repository);

        var result = await handler.Handle(
            new ListTrackedAddressesQuery(TelegramUserId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        TrackedAddressDto item = Assert.Single(result.Value);
        Assert.Equal(TelegramUserId, item.TelegramUserId);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task UpdateHandlerChangesLabelAndNetworksAndWritesEvent()
    {
        var repository = new InMemoryTrackedAddressRepository();
        TrackedAddress trackedAddress = CreateTrackedAddress(TelegramUserId, [1]);
        repository.Add(trackedAddress);
        var outbox = new CapturingOutboxWriter();
        var unitOfWork = new StubUnitOfWork();
        var handler = new UpdateTrackedAddressCommandHandler(
            repository,
            outbox,
            unitOfWork,
            new FixedTimeProvider(FixedNow));

        var result = await handler.Handle(
            new UpdateTrackedAddressCommand(
                trackedAddress.Id,
                TelegramUserId,
                [137],
                "Operations"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Operations", result.Value.Label);
        Assert.Equal([137], result.Value.ChainIds);
        AddressUpdatedV1 updatedEvent = Assert.IsType<AddressUpdatedV1>(Assert.Single(outbox.Events));
        Assert.Equal([137], updatedEvent.ChainIds);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task RemoveHandlerMarksAggregateInactiveAndWritesEvent()
    {
        var repository = new InMemoryTrackedAddressRepository();
        TrackedAddress trackedAddress = CreateTrackedAddress(TelegramUserId, [1]);
        repository.Add(trackedAddress);
        var outbox = new CapturingOutboxWriter();
        var unitOfWork = new StubUnitOfWork();
        var handler = new RemoveTrackedAddressCommandHandler(
            repository,
            outbox,
            unitOfWork,
            new FixedTimeProvider(FixedNow));

        var result = await handler.Handle(
            new RemoveTrackedAddressCommand(trackedAddress.Id, TelegramUserId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(trackedAddress.IsActive);
        Assert.IsType<AddressRemovedV1>(Assert.Single(outbox.Events));
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    private static TrackedAddress CreateTrackedAddress(
        long telegramUserId,
        IReadOnlyCollection<long> chainIds)
    {
        Address address = Address.Create(RawAddress).Value;
        ChainId[] networks = chainIds.Select(value => ChainId.Create(value).Value).ToArray();

        return TrackedAddress.Create(
            address,
            telegramUserId,
            networks,
            telegramChatId: telegramUserId).Value;
    }

    private sealed class InMemoryTrackedAddressRepository : ITrackedAddressRepository
    {
        public List<TrackedAddress> Items { get; } = [];

        public void Add(TrackedAddress trackedAddress) => Items.Add(trackedAddress);

        public Task<TrackedAddress?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task<IReadOnlyList<TrackedAddress>> ListByUserAsync(
            long telegramUserId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrackedAddress>>(
                Items.Where(item =>
                    item.TelegramUserId == telegramUserId && item.IsActive).ToArray());

        public Task<IReadOnlyList<TrackedAddress>> ListByUserAndAddressAsync(
            long telegramUserId,
            Address address,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrackedAddress>>(
                Items.Where(item =>
                    item.TelegramUserId == telegramUserId && item.Address == address).ToArray());

        public Task UpdateAsync(
            TrackedAddress trackedAddress,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingOutboxWriter : IOutboxWriter
    {
        public List<IWatchlistIntegrationEvent> Events { get; } = [];

        public void Add(IWatchlistIntegrationEvent integrationEvent) =>
            Events.Add(integrationEvent);
    }

    private sealed class StubUnitOfWork : IWatchlistUnitOfWork
    {
        public WatchlistCommitResult CommitResult { get; init; } =
            WatchlistCommitResult.Success;

        public int SaveCalls { get; private set; }

        public Task<WatchlistCommitResult> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(CommitResult);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
