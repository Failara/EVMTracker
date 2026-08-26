using EVMTracker.Notification.Domain.Notifications;

namespace EVMTracker.Notification.UnitTests;

public sealed class TransferNotificationTests
{
    private static readonly Guid SubscriptionId =
        Guid.Parse("01991ef2-1234-7d20-83ef-53cd755f09a7");

    [Theory]
    [InlineData("1234500", 6, "1.2345")]
    [InlineData("1", 18, "0.000000000000000001")]
    [InlineData("1000000", 6, "1")]
    [InlineData("42", 0, "42")]
    [InlineData("42", null, "42")]
    public void TokenAmountFormattingIsExact(
        string rawValue,
        int? decimals,
        string expected)
    {
        Assert.Equal(expected, TokenAmountFormatter.Format(rawValue, decimals));
    }

    [Fact]
    public void StableIdentityUsesSubscriptionAndCanonicalTransferIdentity()
    {
        TransferNotification first = CreateNotification(SubscriptionId);
        TransferNotification duplicate = CreateNotification(SubscriptionId);
        TransferNotification anotherSubscription = CreateNotification(Guid.NewGuid());

        Assert.Equal(first.Id, duplicate.Id);
        Assert.NotEqual(first.Id, anotherSubscription.Id);
    }

    [Fact]
    public void CreationNormalizesAddressesAndFormatsAmount()
    {
        TransferNotification notification = CreateNotification(SubscriptionId);

        Assert.Equal(TransferDirection.Incoming, notification.Direction);
        Assert.Equal("1.2345", notification.DisplayAmount);
        Assert.Equal(notification.MonitoredAddress, notification.MonitoredAddress.ToLowerInvariant());
        Assert.Equal(notification.TransactionHash, notification.TransactionHash.ToLowerInvariant());
    }

    private static TransferNotification CreateNotification(Guid subscriptionId) =>
        TransferNotification.Create(
            subscriptionId,
            Guid.NewGuid(),
            42,
            84,
            1,
            "Ethereum",
            "0x2222222222222222222222222222222222222222",
            "Treasury",
            TransferDirection.Incoming,
            "0x9999999999999999999999999999999999999999",
            "USDC",
            "USD Coin",
            6,
            "1234500",
            "0x1111111111111111111111111111111111111111",
            "0x2222222222222222222222222222222222222222",
            "0xAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            100,
            "0xBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
            3,
            new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));
}
