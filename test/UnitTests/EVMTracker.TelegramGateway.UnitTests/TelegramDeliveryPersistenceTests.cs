using EVMTracker.Contracts.Notifications;
using EVMTracker.TelegramGateway.Application.Delivery;
using EVMTracker.TelegramGateway.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.TelegramGateway.UnitTests;

public sealed class TelegramDeliveryPersistenceTests
{
    [Fact]
    public async Task AttemptsAndFinalDeliveryStatusArePersisted()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        var store = new TelegramDeliveryStore(database.Context);
        TelegramDeliveryRequestedV1 request = CreateRequest();
        DateTimeOffset firstAttemptAt = request.OccurredAtUtc.AddSeconds(1);

        await store.RegisterAsync(request, request.OccurredAtUtc, CancellationToken.None);
        int firstAttempt = await store.BeginAttemptAsync(
            request.EventId,
            firstAttemptAt,
            CancellationToken.None);
        await store.CompleteAttemptAsync(
            request.EventId,
            firstAttempt,
            TelegramDeliveryAttemptOutcome.TransientFailure,
            null,
            "temporary outage",
            TimeSpan.FromSeconds(2),
            firstAttemptAt.AddMilliseconds(20),
            CancellationToken.None);
        int secondAttempt = await store.BeginAttemptAsync(
            request.EventId,
            firstAttemptAt.AddSeconds(2),
            CancellationToken.None);
        await store.CompleteAttemptAsync(
            request.EventId,
            secondAttempt,
            TelegramDeliveryAttemptOutcome.Delivered,
            321,
            null,
            null,
            firstAttemptAt.AddSeconds(2.1),
            CancellationToken.None);

        var delivery = await database.Context.TelegramDeliveries.SingleAsync();
        var attempts = await database.Context.TelegramDeliveryAttempts
            .OrderBy(attempt => attempt.AttemptNumber)
            .ToArrayAsync();
        Assert.Equal(TelegramDeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(2, delivery.AttemptCount);
        Assert.Equal(321, delivery.TelegramMessageId);
        Assert.Equal(2, attempts.Length);
        Assert.Equal(TelegramDeliveryAttemptOutcome.TransientFailure, attempts[0].Outcome);
        Assert.Equal(2000, attempts[0].RetryDelayMilliseconds);
        Assert.Equal(TelegramDeliveryAttemptOutcome.Delivered, attempts[1].Outcome);
    }

    [Fact]
    public async Task DuplicateRegistrationReturnsExistingTerminalState()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        var store = new TelegramDeliveryStore(database.Context);
        TelegramDeliveryRequestedV1 request = CreateRequest();
        await store.RegisterAsync(request, request.OccurredAtUtc, CancellationToken.None);
        int attempt = await store.BeginAttemptAsync(
            request.EventId,
            request.OccurredAtUtc,
            CancellationToken.None);
        await store.CompleteAttemptAsync(
            request.EventId,
            attempt,
            TelegramDeliveryAttemptOutcome.Delivered,
            321,
            null,
            null,
            request.OccurredAtUtc,
            CancellationToken.None);

        TelegramDeliveryState duplicate = await store.RegisterAsync(
            request,
            request.OccurredAtUtc.AddMinutes(1),
            CancellationToken.None);

        Assert.Equal(TelegramDeliveryStatus.Delivered, duplicate.Status);
        Assert.Equal(1, await database.Context.TelegramDeliveries.CountAsync());
    }

    [Fact]
    public async Task InterruptedAttemptIsResumedWithoutConsumingAnotherAttempt()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        var store = new TelegramDeliveryStore(database.Context);
        TelegramDeliveryRequestedV1 request = CreateRequest();
        await store.RegisterAsync(request, request.OccurredAtUtc, CancellationToken.None);
        int started = await store.BeginAttemptAsync(
            request.EventId,
            request.OccurredAtUtc,
            CancellationToken.None);

        TelegramDeliveryState recovered = await store.RegisterAsync(
            request,
            request.OccurredAtUtc.AddSeconds(1),
            CancellationToken.None);
        int resumed = await store.BeginAttemptAsync(
            request.EventId,
            request.OccurredAtUtc.AddSeconds(1),
            CancellationToken.None);

        Assert.True(recovered.HasIncompleteAttempt);
        Assert.Equal(started, resumed);
        Assert.Equal(1, await database.Context.TelegramDeliveryAttempts.CountAsync());
    }

    private static TelegramDeliveryRequestedV1 CreateRequest()
    {
        Guid eventId = Guid.Parse("01991ef2-1234-7d20-83ef-53cd755f09a7");
        return new TelegramDeliveryRequestedV1(
            eventId,
            new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero),
            eventId,
            84,
            "<b>Incoming transfer</b>",
            "HTML");
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(
            SqliteConnection connection,
            TelegramGatewayDbContext context)
        {
            this.connection = connection;
            Context = context;
        }

        public TelegramGatewayDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<TelegramGatewayDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new TelegramGatewayDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
