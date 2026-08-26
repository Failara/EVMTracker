using EVMTracker.Watchlist.Domain.ValueObjects;

namespace EVMTracker.Watchlist.UnitTests;

public sealed class AddressTests
{
    public static TheoryData<string, string> ValidAddresses => new()
    {
        {
            "0x52908400098527886e0f7030069857d2e4169ee7",
            "0x52908400098527886E0F7030069857D2E4169EE7"
        },
        {
            "0X8617E340B3D01FA5F11F306F4090FD50E238070D",
            "0x8617E340B3D01FA5F11F306F4090FD50E238070D"
        },
        {
            "0xde709f2102306220921060314715629080e2fb77",
            "0xde709f2102306220921060314715629080e2fb77"
        },
        {
            "0x5AEDA56215b167893e80B4fE645BA6d5Bab767DE",
            "0x5AEDA56215b167893e80B4fE645BA6d5Bab767DE"
        },
        {
            " 0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359 ",
            "0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359"
        },
    };

    [Theory]
    [MemberData(nameof(ValidAddresses))]
    public void CreateNormalizesValidAddressToEip55(string input, string expected)
    {
        var result = Address.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
        Assert.Equal(expected, result.Value.ToString());
    }

    [Theory]
    [InlineData(null, "Address.Required")]
    [InlineData("", "Address.Required")]
    [InlineData("0x1234", "Address.InvalidFormat")]
    [InlineData("52908400098527886E0F7030069857D2E4169EE7", "Address.InvalidFormat")]
    [InlineData("0x52908400098527886E0F7030069857D2E4169EEZ", "Address.InvalidFormat")]
    [InlineData("0x52908400098527886E0F7030069857D2E4169Ee7", "Address.InvalidChecksum")]
    public void CreateRejectsInvalidAddress(string? input, string expectedErrorCode)
    {
        var result = Address.Create(input);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedErrorCode, result.Error.Code);
    }

    [Fact]
    public void EquivalentRepresentationsProduceEqualValues()
    {
        Address lowercase = Address.Create(
            "0x52908400098527886e0f7030069857d2e4169ee7").Value;
        Address uppercase = Address.Create(
            "0x52908400098527886E0F7030069857D2E4169EE7").Value;

        Assert.Equal(lowercase, uppercase);
    }
}
