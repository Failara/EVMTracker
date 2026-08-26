using System.Globalization;
using System.Numerics;
using EVMTracker.EvmIndexer.Application.Decoding;
using EVMTracker.EvmIndexer.Application.Models;

namespace EVMTracker.EvmIndexer.UnitTests;

public sealed class Erc20TransferLogDecoderTests
{
    [Fact]
    public void RealTransferShapeDecodesIndexedParticipantsAndValue()
    {
        RawEvmLog log = CreateTransferLog();

        TransferLogDecodeResult result = Erc20TransferLogDecoder.Decode(1, log);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "0x1111111111111111111111111111111111111111",
            result.Transfer?.FromAddress.Value);
        Assert.Equal(
            "0x2222222222222222222222222222222222222222",
            result.Transfer?.ToAddress.Value);
        Assert.Equal(
            BigInteger.Parse("1000000000000000000", CultureInfo.InvariantCulture),
            result.Transfer?.RawValue);
        Assert.Equal(42, result.Transfer?.BlockNumber);
        Assert.Equal(3, result.Transfer?.LogIndex);
    }

    [Fact]
    public void ContractAddressIsTokenRatherThanParticipant()
    {
        TransferLogDecodeResult result = Erc20TransferLogDecoder.Decode(1, CreateTransferLog());

        Assert.Equal(
            "0x9999999999999999999999999999999999999999",
            result.Transfer?.TokenContract.Value);
        Assert.NotEqual(result.Transfer?.TokenContract, result.Transfer?.FromAddress);
    }

    [Fact]
    public void RemovedLogIsRejected()
    {
        RawEvmLog original = CreateTransferLog();
        RawEvmLog removed = original with { IsRemoved = true };

        TransferLogDecodeResult result = Erc20TransferLogDecoder.Decode(1, removed);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void MalformedTopicIsRejected()
    {
        RawEvmLog original = CreateTransferLog();
        RawEvmLog malformed = original with { Topics = [original.Topics[0], "0x1234"] };

        TransferLogDecodeResult result = Erc20TransferLogDecoder.Decode(1, malformed);

        Assert.False(result.IsSuccess);
    }

    internal static RawEvmLog CreateTransferLog() =>
        new(
            "0x9999999999999999999999999999999999999999",
            [
                Erc20TransferLogDecoder.TransferTopic,
                "0x0000000000000000000000001111111111111111111111111111111111111111",
                "0x0000000000000000000000002222222222222222222222222222222222222222",
            ],
            "0x0000000000000000000000000000000000000000000000000de0b6b3a7640000",
            "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            42,
            "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            3,
            false);
}
