using EVMTracker.Watchlist.Domain.TrackedAddresses;
using EVMTracker.Watchlist.Domain.ValueObjects;

namespace EVMTracker.Watchlist.UnitTests;

public sealed class TrackedAddressTests
{
    private const long OwnerTelegramUserId = 1001;
    private const long OtherTelegramUserId = 2002;

    private static readonly Address TestAddress = Address.Create(
        "0x52908400098527886e0f7030069857d2e4169ee7").Value;

    private static readonly ChainId Ethereum = ChainId.Create(1).Value;
    private static readonly ChainId Polygon = ChainId.Create(137).Value;

    [Fact]
    public void CreateBuildsActiveAggregateAndNormalizesLabel()
    {
        DateTimeOffset beforeCreation = DateTimeOffset.UtcNow;

        var result = TrackedAddress.Create(
            TestAddress,
            OwnerTelegramUserId,
            [Ethereum, Polygon],
            "  Treasury  ");

        DateTimeOffset afterCreation = DateTimeOffset.UtcNow;

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(TestAddress, result.Value.Address);
        Assert.Equal(OwnerTelegramUserId, result.Value.TelegramUserId);
        Assert.Equal(OwnerTelegramUserId, result.Value.TelegramChatId);
        Assert.Equal("Treasury", result.Value.Label);
        Assert.True(result.Value.IsActive);
        Assert.Equal([Ethereum, Polygon], result.Value.EnabledNetworks);
        Assert.InRange(result.Value.CreatedAtUtc, beforeCreation, afterCreation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateRejectsInvalidTelegramUserId(long telegramUserId)
    {
        var result = TrackedAddress.Create(TestAddress, telegramUserId, [Ethereum]);

        Assert.True(result.IsFailure);
        Assert.Equal(TrackedAddressErrors.InvalidTelegramUserId, result.Error);
    }

    [Fact]
    public void CreateStoresExplicitTelegramChatId()
    {
        const long groupChatId = -1001234567890;

        var result = TrackedAddress.Create(
            TestAddress,
            OwnerTelegramUserId,
            [Ethereum],
            telegramChatId: groupChatId);

        Assert.True(result.IsSuccess);
        Assert.Equal(groupChatId, result.Value.TelegramChatId);
    }

    [Fact]
    public void CreateRejectsZeroTelegramChatId()
    {
        var result = TrackedAddress.Create(
            TestAddress,
            OwnerTelegramUserId,
            [Ethereum],
            telegramChatId: 0);

        Assert.True(result.IsFailure);
        Assert.Equal(TrackedAddressErrors.InvalidTelegramChatId, result.Error);
    }

    [Fact]
    public void CreateRequiresAtLeastOneNetwork()
    {
        var result = TrackedAddress.Create(
            TestAddress,
            OwnerTelegramUserId,
            []);

        Assert.True(result.IsFailure);
        Assert.Equal(TrackedAddressErrors.NetworkRequired, result.Error);
    }

    [Fact]
    public void CreateRejectsDuplicateNetworks()
    {
        var result = TrackedAddress.Create(
            TestAddress,
            OwnerTelegramUserId,
            [Ethereum, Ethereum]);

        Assert.True(result.IsFailure);
        Assert.Equal(TrackedAddressErrors.DuplicateNetwork, result.Error);
    }

    [Fact]
    public void CreateRejectsLabelThatExceedsLimit()
    {
        string longLabel = new('a', TrackedAddress.MaximumLabelLength + 1);

        var result = TrackedAddress.Create(
            TestAddress,
            OwnerTelegramUserId,
            [Ethereum],
            longLabel);

        Assert.True(result.IsFailure);
        Assert.Equal(TrackedAddressErrors.LabelTooLong, result.Error);
    }

    [Fact]
    public void OwnerCanRenameAndClearLabel()
    {
        TrackedAddress trackedAddress = CreateTrackedAddress();

        var renameResult = trackedAddress.Rename(OwnerTelegramUserId, "  Operations  ");
        var clearResult = trackedAddress.Rename(OwnerTelegramUserId, "   ");

        Assert.True(renameResult.IsSuccess);
        Assert.True(clearResult.IsSuccess);
        Assert.Null(trackedAddress.Label);
    }

    [Fact]
    public void AnotherUserCannotModifyOrRemoveTrackedAddress()
    {
        TrackedAddress trackedAddress = CreateTrackedAddress();

        var renameResult = trackedAddress.Rename(OtherTelegramUserId, "Unauthorized");
        var enableResult = trackedAddress.EnableNetwork(OtherTelegramUserId, Polygon);
        var removeResult = trackedAddress.Remove(OtherTelegramUserId);

        Assert.Equal(TrackedAddressErrors.Unauthorized, renameResult.Error);
        Assert.Equal(TrackedAddressErrors.Unauthorized, enableResult.Error);
        Assert.Equal(TrackedAddressErrors.Unauthorized, removeResult.Error);
        Assert.True(trackedAddress.IsActive);
        Assert.Equal("Treasury", trackedAddress.Label);
        Assert.False(trackedAddress.IsEnabledOn(Polygon));
    }

    [Fact]
    public void NetworkChangesPreserveActiveNetworkInvariant()
    {
        TrackedAddress trackedAddress = CreateTrackedAddress();

        var enableResult = trackedAddress.EnableNetwork(OwnerTelegramUserId, Polygon);
        var duplicateResult = trackedAddress.EnableNetwork(OwnerTelegramUserId, Polygon);
        var disableEthereumResult = trackedAddress.DisableNetwork(OwnerTelegramUserId, Ethereum);
        var disableLastResult = trackedAddress.DisableNetwork(OwnerTelegramUserId, Polygon);

        Assert.True(enableResult.IsSuccess);
        Assert.Equal(TrackedAddressErrors.NetworkAlreadyEnabled, duplicateResult.Error);
        Assert.True(disableEthereumResult.IsSuccess);
        Assert.Equal(TrackedAddressErrors.LastNetworkCannotBeDisabled, disableLastResult.Error);
        Assert.True(trackedAddress.IsEnabledOn(Polygon));
        Assert.Single(trackedAddress.EnabledNetworks);
    }

    [Fact]
    public void RemovedAddressCannotBeModifiedOrMatched()
    {
        TrackedAddress trackedAddress = CreateTrackedAddress();

        var removeResult = trackedAddress.Remove(OwnerTelegramUserId);
        var secondRemoveResult = trackedAddress.Remove(OwnerTelegramUserId);
        var renameResult = trackedAddress.Rename(OwnerTelegramUserId, "New label");

        Assert.True(removeResult.IsSuccess);
        Assert.False(trackedAddress.IsActive);
        Assert.False(trackedAddress.IsEnabledOn(Ethereum));
        Assert.Equal(TrackedAddressErrors.AlreadyRemoved, secondRemoveResult.Error);
        Assert.Equal(TrackedAddressErrors.Inactive, renameResult.Error);
    }

    [Fact]
    public void DuplicateActiveSubscriptionIsRejectedByDomainPolicy()
    {
        TrackedAddress existing = CreateTrackedAddress();

        var result = TrackedAddressPolicy.EnsureCanTrack(
            OwnerTelegramUserId,
            TestAddress,
            Ethereum,
            [existing]);

        Assert.True(result.IsFailure);
        Assert.Equal(TrackedAddressErrors.AlreadyTracked, result.Error);
    }

    [Fact]
    public void RemovedSubscriptionCanBeTrackedAgain()
    {
        TrackedAddress existing = CreateTrackedAddress();
        existing.Remove(OwnerTelegramUserId);

        var result = TrackedAddressPolicy.EnsureCanTrack(
            OwnerTelegramUserId,
            TestAddress,
            Ethereum,
            [existing]);

        Assert.True(result.IsSuccess);
    }

    private static TrackedAddress CreateTrackedAddress() =>
        TrackedAddress.Create(
            TestAddress,
            OwnerTelegramUserId,
            [Ethereum],
            "Treasury").Value;
}
