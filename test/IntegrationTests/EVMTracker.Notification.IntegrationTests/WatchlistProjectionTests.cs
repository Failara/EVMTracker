using EVMTracker.Contracts;
using EVMTracker.Contracts.Watchlists;
using EVMTracker.Messaging.Publishing;
using EVMTracker.Notification.Application.Watchlists;
using EVMTracker.Notification.Infrastructure.Persistence;
using EVMTracker.Notification.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.Notification.IntegrationTests;

public sealed class WatchlistProjectionTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DuplicateEventDoesNotDuplicateProjectedAddresses()
    {
        await using ProjectionDatabase database = await ProjectionDatabase.CreateAsync();
        var store = new WatchlistProjectionStore(database.Context);
        var projector = new WatchlistProjectionProjector(store);
        AddressAddedV1 added = CreateAdded(Guid.NewGuid(), Guid.NewGuid(), [1, 11155111]);
        WatchlistIntegrationEventEnvelope envelope = CreateEnvelope(added);

        await projector.ProjectAsync(envelope, CancellationToken.None);
        await projector.ProjectAsync(envelope, CancellationToken.None);

        Assert.Equal(2, (await store.ListAsync(CancellationToken.None)).Count);
        Assert.Equal(1, await database.Context.InboxMessages.CountAsync());
    }

    [Fact]
    public async Task ReplayProducesTheSameFinalProjection()
    {
        await using ProjectionDatabase database = await ProjectionDatabase.CreateAsync();
        var store = new WatchlistProjectionStore(database.Context);
        var projector = new WatchlistProjectionProjector(store);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        IWatchlistIntegrationEvent[] history =
        [
            CreateAdded(Guid.NewGuid(), firstId, [1]),
            CreateAdded(Guid.NewGuid(), secondId, [1, 10]),
            new AddressUpdatedV1(
                Guid.NewGuid(),
                OccurredAt.AddMinutes(1),
                firstId,
                42,
                84,
                "0x0000000000000000000000000000000000000001",
                "Updated",
                [10, 137]),
            new AddressRemovedV1(
                Guid.NewGuid(),
                OccurredAt.AddMinutes(2),
                secondId,
                42,
                84,
                "0x0000000000000000000000000000000000000001"),
        ];

        foreach (IWatchlistIntegrationEvent integrationEvent in history)
        {
            await projector.ProjectAsync(CreateEnvelope(integrationEvent), CancellationToken.None);
        }

        ProjectedWatchlistEntry[] expected =
            (await store.ListAsync(CancellationToken.None)).ToArray();
        Guid transferInboxId = Guid.NewGuid();
        database.Context.InboxMessages.Add(
            new InboxMessage
            {
                EventId = transferInboxId,
                EventType = "transfers.erc20-observed.v1",
                ProcessedAtUtc = OccurredAt,
            });
        await database.Context.SaveChangesAsync();
        var reset = new WatchlistProjectionResetV1(
            Guid.NewGuid(),
            OccurredAt.AddMinutes(3),
            Guid.NewGuid());
        await projector.ProjectAsync(CreateEnvelope(reset), CancellationToken.None);
        foreach (IWatchlistIntegrationEvent integrationEvent in history)
        {
            await projector.ProjectAsync(CreateEnvelope(integrationEvent), CancellationToken.None);
        }

        Assert.Equal(expected, (await store.ListAsync(CancellationToken.None)).ToArray());
        Assert.True(
            await database.Context.InboxMessages.AnyAsync(
                message => message.EventId == transferInboxId));
    }

    [Fact]
    public async Task PoisonedVersionFailsAndAValidLaterEventCanStillBeApplied()
    {
        await using ProjectionDatabase database = await ProjectionDatabase.CreateAsync();
        var store = new WatchlistProjectionStore(database.Context);
        var projector = new WatchlistProjectionProjector(store);
        AddressAddedV1 added = CreateAdded(Guid.NewGuid(), Guid.NewGuid(), [1]);
        WatchlistIntegrationEventEnvelope valid = CreateEnvelope(added);
        WatchlistIntegrationEventEnvelope poisoned = valid with { EventVersion = 99 };

        await Assert.ThrowsAsync<UnsupportedWatchlistEventException>(
            () => projector.ProjectAsync(poisoned, CancellationToken.None));
        await projector.ProjectAsync(valid, CancellationToken.None);

        Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.Equal(1, await database.Context.InboxMessages.CountAsync());
    }

    private static AddressAddedV1 CreateAdded(
        Guid eventId,
        Guid trackedAddressId,
        IReadOnlyCollection<long> chainIds) =>
        new(
            eventId,
            OccurredAt,
            trackedAddressId,
            42,
            84,
            "0x0000000000000000000000000000000000000001",
            "Treasury",
            chainIds);

    private static WatchlistIntegrationEventEnvelope CreateEnvelope(
        IWatchlistIntegrationEvent integrationEvent)
    {
        IntegrationEventEnvelope envelope = IntegrationEventEnvelopeFactory.Create(integrationEvent);
        return new WatchlistIntegrationEventEnvelope(
            envelope.EventId,
            envelope.EventType,
            envelope.EventVersion,
            envelope.OccurredAt,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.Payload);
    }

    private sealed class ProjectionDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ProjectionDatabase(
            SqliteConnection connection,
            NotificationDbContext context)
        {
            this.connection = connection;
            Context = context;
        }

        public NotificationDbContext Context { get; }

        public static async Task<ProjectionDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new NotificationDbContext(options);
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
