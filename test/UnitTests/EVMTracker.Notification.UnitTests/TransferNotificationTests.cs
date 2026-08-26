using EVMTracker.Notification.Application.Transfers;
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

    [Theory]
    [InlineData(TransferDirection.Incoming, "<b>Incoming ERC-20 transfer</b>")]
    [InlineData(TransferDirection.Outgoing, "<b>Outgoing ERC-20 transfer</b>")]
    public void RendererIncludesDirectionExplorerLinksAndConfirmation(
        TransferDirection direction,
        string expectedHeading)
    {
        TransferNotification notification = CreateNotification(
            SubscriptionId,
            direction: direction);

        string message = TransferNotificationRenderer.Render(
            notification,
            "https://etherscan.io");

        Assert.Contains(expectedHeading, message, StringComparison.Ordinal);
        Assert.Contains("https://etherscan.io/address/", message, StringComparison.Ordinal);
        Assert.Contains("https://etherscan.io/token/", message, StringComparison.Ordinal);
        Assert.Contains("https://etherscan.io/tx/", message, StringComparison.Ordinal);
        Assert.Contains("Block: 100 · Confirmed", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RendererEscapesUserAndTokenMetadataForTelegramHtml()
    {
        TransferNotification notification = CreateNotification(
            SubscriptionId,
            label: "Ops <hot> & treasury",
            tokenSymbol: "A&B",
            tokenName: "Alpha <Beta>");

        string message = TransferNotificationRenderer.Render(notification, null);

        Assert.Contains("Ops &lt;hot&gt; &amp; treasury", message, StringComparison.Ordinal);
        Assert.Contains("Alpha &lt;Beta&gt; (A&amp;B)", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Ops <hot>", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RendererKeepsUnknownTokenNotificationUsefulWithoutExplorer()
    {
        TransferNotification notification = CreateNotification(
            SubscriptionId,
            tokenSymbol: "TOKEN-999999",
            tokenName: null,
            tokenDecimals: null);

        string message = TransferNotificationRenderer.Render(notification, null);

        Assert.Contains("1234500 TOKEN-999999", message, StringComparison.Ordinal);
        Assert.Contains("Token: TOKEN-999999", message, StringComparison.Ordinal);
        Assert.Contains("Transaction: 0xaaaaaa", message, StringComparison.Ordinal);
    }

    private static TransferNotification CreateNotification(
        Guid subscriptionId,
        TransferDirection direction = TransferDirection.Incoming,
        string? label = "Treasury",
        string tokenSymbol = "USDC",
        string? tokenName = "USD Coin",
        int? tokenDecimals = 6) =>
        TransferNotification.Create(
            subscriptionId,
            Guid.NewGuid(),
            42,
            84,
            1,
            "Ethereum",
            "0x2222222222222222222222222222222222222222",
            label,
            direction,
            "0x9999999999999999999999999999999999999999",
            tokenSymbol,
            tokenName,
            tokenDecimals,
            "1234500",
            "0x1111111111111111111111111111111111111111",
            "0x2222222222222222222222222222222222222222",
            "0xAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            100,
            "0xBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
            3,
            new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));
}
