using System.Text.Json;
using EVMTracker.Contracts.Watchlists;
using EVMTracker.EvmIndexer.Application.Watchlists;
using EVMTracker.EvmIndexer.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.EvmIndexer.IntegrationTests;

public sealed class IndexerWatchlistProjectionTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProjectionFiltersByChainAndTracksLifecycle()
    {
        await using ProjectionDatabase database = await ProjectionDatabase.CreateAsync();
        var store = new IndexerWatchlistProjectionStore(database.Context);
        var projector = new IndexerWatchlistProjectionProjector(store);
        Guid trackedAddressId = Guid.NewGuid();
        const string address = "0x1111111111111111111111111111111111111111";

        var added = new AddressAddedV1(
            Guid.NewGuid(),
            Now,
            trackedAddressId,
            42,
            84,
            address,
            null,
            [1, 56]);
        await projector.ProjectAsync(Envelope(added), CancellationToken.None);
        await projector.ProjectAsync(Envelope(added), CancellationToken.None);

        Assert.Equal([address], await store.ListAsync(56, CancellationToken.None));
        Assert.Equal([address], await store.ListAsync(1, CancellationToken.None));
        Assert.Empty(await store.ListAsync(137, CancellationToken.None));
        Assert.Equal(2, await database.Context.WatchlistProjection.CountAsync());
        Assert.Single(database.Context.WatchlistInboxMessages);

        var updated = new AddressUpdatedV1(
            Guid.NewGuid(),
            Now.AddMinutes(1),
            trackedAddressId,
            42,
            84,
            address,
            "Treasury",
            [56]);
        await projector.ProjectAsync(Envelope(updated), CancellationToken.None);

        Assert.Empty(await store.ListAsync(1, CancellationToken.None));
        Assert.Equal([address], await store.ListAsync(56, CancellationToken.None));

        var removed = new AddressRemovedV1(
            Guid.NewGuid(),
            Now.AddMinutes(2),
            trackedAddressId,
            42,
            84,
            address);
        await projector.ProjectAsync(Envelope(removed), CancellationToken.None);

        Assert.Empty(await store.ListAsync(56, CancellationToken.None));
    }

    [Fact]
    public async Task ProjectionResetClearsExistingEntriesAndAllowsReplay()
    {
        await using ProjectionDatabase database = await ProjectionDatabase.CreateAsync();
        var store = new IndexerWatchlistProjectionStore(database.Context);
        var projector = new IndexerWatchlistProjectionProjector(store);
        var added = new AddressAddedV1(
            Guid.NewGuid(),
            Now,
            Guid.NewGuid(),
            42,
            84,
            "0x1111111111111111111111111111111111111111",
            null,
            [56]);
        await projector.ProjectAsync(Envelope(added), CancellationToken.None);

        var reset = new WatchlistProjectionResetV1(
            Guid.NewGuid(),
            Now.AddMinutes(1),
            Guid.NewGuid());
        await projector.ProjectAsync(Envelope(reset), CancellationToken.None);
        await projector.ProjectAsync(Envelope(added), CancellationToken.None);

        Assert.Equal(
            [added.Address],
            await store.ListAsync(56, CancellationToken.None));
        Assert.Equal(2, await database.Context.WatchlistInboxMessages.CountAsync());
    }

    private static WatchlistIntegrationEventEnvelope Envelope(
        IWatchlistIntegrationEvent integrationEvent) =>
        new(
            integrationEvent.EventId,
            integrationEvent.EventType,
            integrationEvent.EventVersion,
            integrationEvent.OccurredAtUtc,
            integrationEvent.EventId,
            null,
            JsonSerializer.SerializeToElement(
                integrationEvent,
                integrationEvent.GetType(),
                SerializerOptions));

    private sealed class ProjectionDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ProjectionDatabase(SqliteConnection connection, IndexerDbContext context)
        {
            this.connection = connection;
            Context = context;
        }

        public IndexerDbContext Context { get; }

        public static async Task<ProjectionDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<IndexerDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new IndexerDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new ProjectionDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
