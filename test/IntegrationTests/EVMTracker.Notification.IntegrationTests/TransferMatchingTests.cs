using System.Text.Json;
using EVMTracker.Contracts;
using EVMTracker.Contracts.Notifications;
using EVMTracker.Contracts.Transfers;
using EVMTracker.Contracts.Watchlists;
using EVMTracker.Messaging.Publishing;
using EVMTracker.Notification.Application.Transfers;
using EVMTracker.Notification.Domain.Notifications;
using EVMTracker.Notification.Infrastructure.Persistence;
using EVMTracker.Notification.Infrastructure.Persistence.Configurations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.Notification.IntegrationTests;

public sealed class TransferMatchingTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private const string MonitoredAddress =
        "0x2222222222222222222222222222222222222222";
    private const string OtherAddress =
        "0x1111111111111111111111111111111111111111";
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(OtherAddress, MonitoredAddress, TransferDirection.Incoming)]
    [InlineData(MonitoredAddress, OtherAddress, TransferDirection.Outgoing)]
    [InlineData(MonitoredAddress, MonitoredAddress, TransferDirection.Self)]
    public async Task ParticipantMatchCreatesNotificationWithCorrectDirection(
        string fromAddress,
        string toAddress,
        TransferDirection expectedDirection)
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await SeedSubscriptionAsync(database.Context, Guid.NewGuid(), MonitoredAddress, 42, 84);
        TransferEventProcessor processor = CreateProcessor(database.Context);

        TransferProcessingResult result = await processor.ProcessAsync(
            CreateTransferEnvelope(Guid.NewGuid(), fromAddress, toAddress),
            CancellationToken.None);

        Assert.Equal(new TransferProcessingResult(1, 1), result);
        var notification = await database.Context.TransferNotifications.SingleAsync();
        Assert.Equal(expectedDirection, notification.Direction);
        Assert.Equal("1.2345", notification.DisplayAmount);
        Assert.Equal("Ethereum", notification.NetworkName);
        var outbox = await database.Context.OutboxMessages.SingleAsync();
        Assert.Equal("notifications.telegram-delivery-requested.v1", outbox.EventType);
        Assert.Equal(notification.SourceEventId, outbox.CausationId);
        TelegramDeliveryRequestedV1 delivery = JsonSerializer.Deserialize<
            TelegramDeliveryRequestedV1>(
                outbox.Payload,
                SerializerOptions)!;
        Assert.Equal("HTML", delivery.ParseMode);
        Assert.Contains(
            "https://etherscan.io/tx/",
            delivery.Message,
            StringComparison.Ordinal);
        Assert.Contains("Confirmed", delivery.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnrelatedTransferIsIgnoredButMarkedAsConsumed()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await SeedSubscriptionAsync(database.Context, Guid.NewGuid(), MonitoredAddress, 42, 84);
        TransferEventProcessor processor = CreateProcessor(database.Context);

        TransferProcessingResult result = await processor.ProcessAsync(
            CreateTransferEnvelope(Guid.NewGuid(), OtherAddress, TokenAddress),
            CancellationToken.None);

        Assert.Equal(new TransferProcessingResult(0, 0), result);
        Assert.Equal(0, await database.Context.TransferNotifications.CountAsync());
        Assert.Equal(0, await database.Context.OutboxMessages.CountAsync());
        Assert.Equal(2, await database.Context.InboxMessages.CountAsync());
    }

    [Fact]
    public async Task MultipleSubscriptionsCreateSeparateUserNotifications()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await SeedSubscriptionAsync(database.Context, Guid.NewGuid(), MonitoredAddress, 42, 84);
        await SeedSubscriptionAsync(database.Context, Guid.NewGuid(), MonitoredAddress, 43, 85);
        TransferEventProcessor processor = CreateProcessor(database.Context);

        TransferProcessingResult result = await processor.ProcessAsync(
            CreateTransferEnvelope(Guid.NewGuid(), OtherAddress, MonitoredAddress),
            CancellationToken.None);

        Assert.Equal(new TransferProcessingResult(2, 2), result);
        Assert.Equal(2, await database.Context.TransferNotifications.CountAsync());
        Assert.Equal(2, await database.Context.OutboxMessages.CountAsync());
        Assert.Equal(
            [84L, 85L],
            await database.Context.TransferNotifications
                .OrderBy(record => record.TelegramChatId)
                .Select(record => record.TelegramChatId)
                .ToArrayAsync());
    }

    [Fact]
    public async Task DuplicateBrokerDeliveryDoesNotDuplicateLogicalNotification()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await SeedSubscriptionAsync(database.Context, Guid.NewGuid(), MonitoredAddress, 42, 84);
        TransferEventProcessor processor = CreateProcessor(database.Context);
        TransferIntegrationEventEnvelope envelope = CreateTransferEnvelope(
            Guid.NewGuid(),
            OtherAddress,
            MonitoredAddress);

        TransferProcessingResult first = await processor.ProcessAsync(
            envelope,
            CancellationToken.None);
        TransferProcessingResult duplicate = await processor.ProcessAsync(
            envelope,
            CancellationToken.None);
        TransferProcessingResult alternateEventIdentity = await processor.ProcessAsync(
            CreateTransferEnvelope(Guid.NewGuid(), OtherAddress, MonitoredAddress),
            CancellationToken.None);

        Assert.Equal(1, first.CreatedNotifications);
        Assert.Equal(0, duplicate.CreatedNotifications);
        Assert.Equal(0, alternateEventIdentity.CreatedNotifications);
        Assert.Equal(1, await database.Context.TransferNotifications.CountAsync());
        Assert.Equal(1, await database.Context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task ModelDefinesStableLogicalNotificationConstraint()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        string[] indexNames = database.Context.Model
            .FindEntityType(
                "EVMTracker.Notification.Infrastructure.Persistence.Entities.TransferNotificationRecord")!
            .GetIndexes()
            .Select(index => index.GetDatabaseName())
            .OfType<string>()
            .ToArray();

        Assert.Contains(
            TransferNotificationRecordConfiguration.UniqueLogicalNotificationConstraintName,
            indexNames);
    }

    private const string TokenAddress =
        "0x9999999999999999999999999999999999999999";

    private static TransferEventProcessor CreateProcessor(NotificationDbContext context) =>
        new(
            new WatchlistProjectionStore(context),
            new TransferNotificationStore(context),
            new TestNetworkResolver());

    private static async Task SeedSubscriptionAsync(
        NotificationDbContext context,
        Guid subscriptionId,
        string address,
        long userId,
        long chatId)
    {
        var store = new WatchlistProjectionStore(context);
        Guid eventId = Guid.NewGuid();
        await store.ApplyAddedAsync(
            eventId,
            OccurredAt,
            new AddressAddedV1(
                eventId,
                OccurredAt,
                subscriptionId,
                userId,
                chatId,
                address,
                "Treasury",
                [1]),
            CancellationToken.None);
    }

    private static TransferIntegrationEventEnvelope CreateTransferEnvelope(
        Guid eventId,
        string fromAddress,
        string toAddress)
    {
        var transfer = new Erc20TransferObservedV1(
            eventId,
            OccurredAt,
            1,
            TokenAddress,
            fromAddress,
            toAddress,
            "1234500",
            "USDC",
            "USD Coin",
            6,
            "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            100,
            "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            3);
        IntegrationEventEnvelope envelope = IntegrationEventEnvelopeFactory.Create(transfer);
        return new TransferIntegrationEventEnvelope(
            envelope.EventId,
            envelope.EventType,
            envelope.EventVersion,
            envelope.OccurredAt,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.Payload);
    }

    private sealed class TestNetworkResolver : INetworkPresentationResolver
    {
        public NetworkPresentation Resolve(long chainId) => new(
            chainId == 1 ? "Ethereum" : $"Chain {chainId}",
            chainId == 1 ? "https://etherscan.io" : null);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(SqliteConnection connection, NotificationDbContext context)
        {
            this.connection = connection;
            Context = context;
        }

        public NotificationDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new NotificationDbContext(options);
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
