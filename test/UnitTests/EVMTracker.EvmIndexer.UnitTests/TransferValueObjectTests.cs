using EVMTracker.EvmIndexer.Domain.Indexing;
using EVMTracker.EvmIndexer.Domain.Transfers;

namespace EVMTracker.EvmIndexer.UnitTests;

public sealed class TransferValueObjectTests
{
    [Fact]
    public void AddressNormalizesToLowercase()
    {
        bool success = EvmAddress.TryCreate(
            "0x52908400098527886E0F7030069857D2E4169EE7",
            out EvmAddress? address);

        Assert.True(success);
        Assert.Equal(
            "0x52908400098527886e0f7030069857d2e4169ee7",
            address?.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0x1234")]
    [InlineData("0x52908400098527886E0F7030069857D2E4169EEZ")]
    public void MalformedAddressIsRejected(string? value)
    {
        bool success = EvmAddress.TryCreate(value, out EvmAddress? address);

        Assert.False(success);
        Assert.Null(address);
    }

    [Fact]
    public void BlockRangeStopsAtSafeHead()
    {
        BlockRange range = BlockRange.Create(100, 103, 20);

        Assert.Equal(100, range.FromBlock);
        Assert.Equal(103, range.ToBlock);
    }

    [Fact]
    public void SafeHeadSubtractsConfirmationDepth()
    {
        Assert.Equal(988, BlockRange.CalculateSafeHead(1000, 12));
    }
}
