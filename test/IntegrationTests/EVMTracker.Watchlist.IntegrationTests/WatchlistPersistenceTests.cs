using EVMTracker.Contracts.Watchlists;
using EVMTracker.Watchlist.Application.Abstractions.Persistence;
using EVMTracker.Watchlist.Domain.TrackedAddresses;
using EVMTracker.Watchlist.Domain.ValueObjects;
using EVMTracker.Watchlist.Infrastructure.Persistence;
using EVMTracker.Watchlist.Infrastructure.Persistence.Configurations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EVMTracker.Watchlist.IntegrationTests;

public sealed class WatchlistPersistenceTests
{
    private const long TelegramUserId = 1001;
    private const string RawAddress = "0x52908400098527886e0f7030069857d2e4169ee7";

    [Fact]
    public async Task RepositoryCanSaveAndReloadTrackedAddress()
    {
        using var database = new SqliteWatchlistDatabase();
        var repository = new TrackedAddressRepository(database.Context);
        var unitOfWork = new WatchlistUnitOfWork(database.Context);
        TrackedAddress expected = CreateTrackedAddress([1, 137], "Treasury");

        repository.Add(expected);
        WatchlistCommitResult commitResult = await unitOfWork.SaveChangesAsync(
            CancellationToken.None);
        database.Context.ChangeTracker.Clear();

        TrackedAddress? actual = await repository.GetByIdAsync(
            expected.Id,
            CancellationToken.None);

        Assert.Equal(WatchlistCommitResult.Success, commitResult);
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Address, actual.Address);
        Assert.Equal(expected.TelegramUserId, actual.TelegramUserId);
        Assert.Equal(expected.TelegramChatId, actual.TelegramChatId);
        Assert.Equal(expected.Label, actual.Label);
        Assert.Equal(expected.EnabledNetworks, actual.EnabledNetworks);
        Assert.True(actual.IsActive);
    }

    [Fact]
    public async Task DatabaseConstraintRejectsDuplicateUserAddressAndNetwork()
    {
        using var database = new SqliteWatchlistDatabase();
        var repository = new TrackedAddressRepository(database.Context);
        var unitOfWork = new WatchlistUnitOfWork(database.Context);
        TrackedAddress first = CreateTrackedAddress([1], "First");
        TrackedAddress duplicate = CreateTrackedAddress([1], "Duplicate");

        repository.Add(first);
        Assert.Equal(
            WatchlistCommitResult.Success,
            await unitOfWork.SaveChangesAsync(CancellationToken.None));

        repository.Add(duplicate);
        WatchlistCommitResult duplicateResult = await unitOfWork.SaveChangesAsync(
            CancellationToken.None);

        Assert.Equal(WatchlistCommitResult.DuplicateTrackedAddress, duplicateResult);
        Assert.Equal(1, await database.Context.TrackedAddresses.CountAsync());
    }

    [Fact]
    public async Task AggregateAndOutboxMessageCommitAtomically()
    {
        using var database = new SqliteWatchlistDatabase();
        var repository = new TrackedAddressRepository(database.Context);
        var outbox = new OutboxWriter(database.Context);
        var unitOfWork = new WatchlistUnitOfWork(database.Context);
        TrackedAddress trackedAddress = CreateTrackedAddress([1], "Treasury");
        var integrationEvent = new AddressAddedV1(
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            trackedAddress.Id,
            trackedAddress.TelegramUserId,
            trackedAddress.TelegramChatId,
            trackedAddress.Address.Value,
            trackedAddress.Label,
            [1]);

        repository.Add(trackedAddress);
        outbox.Add(integrationEvent);
        WatchlistCommitResult commitResult = await unitOfWork.SaveChangesAsync(
            CancellationToken.None);

        Assert.Equal(WatchlistCommitResult.Success, commitResult);
        Assert.Equal(1, await database.Context.TrackedAddresses.CountAsync());
        Assert.Equal(1, await database.Context.OutboxMessages.CountAsync());
        Assert.Equal(
            WatchlistEventTypes.AddressAddedV1,
            await database.Context.OutboxMessages
                .Select(message => message.EventType)
                .SingleAsync());
    }

    [Fact]
    public void ModelContainsRequiredUniqueTrackingConstraint()
    {
        using var database = new SqliteWatchlistDatabase();
        IEntityType networkEntity = database.Context.Model.FindEntityType(
            "EVMTracker.Watchlist.Infrastructure.Persistence.Entities.TrackedAddressNetworkRecord")!;

        IIndex? uniqueIndex = networkEntity.GetIndexes().SingleOrDefault(index =>
            string.Equals(
                index.GetDatabaseName(),
                TrackedAddressNetworkRecordConfiguration.UniqueTrackingConstraintName,
                StringComparison.Ordinal));

        Assert.NotNull(uniqueIndex);
        Assert.True(uniqueIndex.IsUnique);
        Assert.Equal(
            ["TelegramUserId", "Address", "ChainId"],
            uniqueIndex.Properties.Select(property => property.Name));
    }

    private static TrackedAddress CreateTrackedAddress(
        IReadOnlyCollection<long> chainIds,
        string label)
    {
        Address address = Address.Create(RawAddress).Value;
        ChainId[] networks = chainIds.Select(value => ChainId.Create(value).Value).ToArray();

        return TrackedAddress.Create(
            address,
            TelegramUserId,
            networks,
            label,
            telegramChatId: -100200300).Value;
    }

    private sealed class SqliteWatchlistDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;

        public SqliteWatchlistDatabase()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<WatchlistDbContext>()
                .UseSqlite(_connection)
                .Options;
            Context = new WatchlistDbContext(options);
            Context.Database.EnsureCreated();
        }

        public WatchlistDbContext Context { get; }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
