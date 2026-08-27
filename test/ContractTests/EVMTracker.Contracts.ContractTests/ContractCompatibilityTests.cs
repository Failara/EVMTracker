using System.Text.Json;
using EVMTracker.Contracts;
using EVMTracker.Contracts.Notifications;
using EVMTracker.Contracts.Transfers;
using EVMTracker.Contracts.Watchlists;
using EVMTracker.Messaging.Publishing;

namespace EVMTracker.Contracts.ContractTests;

public sealed class ContractCompatibilityTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly string[] EnvelopeProperties =
    [
        "causationId",
        "correlationId",
        "eventId",
        "eventType",
        "eventVersion",
        "occurredAt",
        "payload",
    ];

    private static readonly string[] AddressAddedProperties =
    [
        "address",
        "chainIds",
        "eventId",
        "eventType",
        "eventVersion",
        "label",
        "occurredAtUtc",
        "telegramChatId",
        "telegramUserId",
        "trackedAddressId",
    ];

    private static readonly string[] TelegramDeliveryProperties =
    [
        "eventId",
        "eventType",
        "eventVersion",
        "message",
        "notificationId",
        "occurredAtUtc",
        "parseMode",
        "telegramChatId",
    ];

    private static readonly string[] TransferOrphanedProperties =
    [
        "chainId",
        "eventId",
        "eventType",
        "eventVersion",
        "formerBlockHash",
        "logIndex",
        "occurredAtUtc",
        "transactionHash",
    ];

    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VersionedEnvelopeKeepsRequiredWireFields()
    {
        var integrationEvent = new AddressAddedV1(
            Guid.Parse("0198e100-0000-7000-8000-000000000001"),
            OccurredAt,
            Guid.Parse("0198e100-0000-7000-8000-000000000002"),
            42,
            84,
            "0x0000000000000000000000000000000000000001",
            "Treasury",
            [1, 11155111]);

        IntegrationEventEnvelope envelope = IntegrationEventEnvelopeFactory.Create(
            integrationEvent,
            Guid.Parse("0198e100-0000-7000-8000-000000000003"),
            Guid.Parse("0198e100-0000-7000-8000-000000000004"));
        using JsonDocument document = JsonDocument.Parse(
            JsonSerializer.Serialize(envelope, SerializerOptions));

        string[] properties = document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(EnvelopeProperties, properties);
        Assert.Equal(1, envelope.EventVersion);
        Assert.Equal(WatchlistEventTypes.AddressAddedV1, envelope.EventType);
    }

    [Fact]
    public void StableEventTypeNamesRemainVersioned()
    {
        Assert.Equal("watchlist.address-added.v1", WatchlistEventTypes.AddressAddedV1);
        Assert.Equal("watchlist.address-updated.v1", WatchlistEventTypes.AddressUpdatedV1);
        Assert.Equal("watchlist.address-removed.v1", WatchlistEventTypes.AddressRemovedV1);
        Assert.Equal("watchlist.projection-reset.v1", WatchlistEventTypes.ProjectionResetV1);
        Assert.Equal("transfers.erc20-observed.v1", TransferEventTypes.Erc20TransferObservedV1);
        Assert.Equal("transfers.erc20-orphaned.v1", TransferEventTypes.Erc20TransferOrphanedV1);
        Assert.Equal(
            "notifications.telegram-delivery-requested.v1",
            NotificationEventTypes.TelegramDeliveryRequestedV1);
    }

    [Fact]
    public void AddressAddedPayloadKeepsRequiredWireFields()
    {
        var integrationEvent = new AddressAddedV1(
            Guid.NewGuid(),
            OccurredAt,
            Guid.NewGuid(),
            42,
            84,
            "0x0000000000000000000000000000000000000001",
            null,
            [1]);
        JsonElement payload = JsonSerializer.SerializeToElement(
            integrationEvent,
            SerializerOptions);

        string[] properties = payload.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(AddressAddedProperties, properties);
    }

    [Fact]
    public void TelegramDeliveryPayloadKeepsRequiredWireFields()
    {
        Guid eventId = Guid.NewGuid();
        var integrationEvent = new TelegramDeliveryRequestedV1(
            eventId,
            OccurredAt,
            Guid.NewGuid(),
            84,
            "<b>Incoming transfer</b>",
            "HTML");
        JsonElement payload = JsonSerializer.SerializeToElement(
            integrationEvent,
            SerializerOptions);

        string[] properties = payload.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(TelegramDeliveryProperties, properties);
    }

    [Fact]
    public void TransferOrphanedPayloadKeepsRequiredWireFields()
    {
        var integrationEvent = new Erc20TransferOrphanedV1(
            Guid.NewGuid(),
            OccurredAt,
            1,
            "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            3,
            "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        JsonElement payload = JsonSerializer.SerializeToElement(
            integrationEvent,
            SerializerOptions);

        string[] properties = payload.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(TransferOrphanedProperties, properties);
    }
}
