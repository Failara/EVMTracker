using EVMTracker.TelegramGateway.Application.Commands;

namespace EVMTracker.TelegramGateway.UnitTests;

public sealed class BotCommandParserTests
{
    [Theory]
    [InlineData("0x52908400098527886e0f7030069857d2e4169ee7")]
    [InlineData("0X52908400098527886E0F7030069857D2E4169EE7")]
    [InlineData("0x52908400098527886E0F7030069857D2E4169EE7")]
    public void AddParsesSupportedAddressCasing(string address)
    {
        BotCommandParseResult result = BotCommandParser.Parse(
            $"/add ethereum {address} Treasury Wallet");

        BotCommand.Add command = Assert.IsType<BotCommand.Add>(result.Command);
        Assert.Equal("ethereum", command.Network);
        Assert.Equal(address, command.Address);
        Assert.Equal("Treasury Wallet", command.Label);
    }

    [Fact]
    public void CommandAddressedToBotParses()
    {
        BotCommandParseResult result = BotCommandParser.Parse("/list@evm_tracker_bot");

        Assert.True(result.IsSuccess);
        Assert.IsType<BotCommand.List>(result.Command);
    }

    [Theory]
    [InlineData("/add", "Usage: /add <network> <0x-address> [label]")]
    [InlineData("/add ethereum", "Usage: /add <network> <0x-address> [label]")]
    [InlineData("/remove", "Usage: /remove <network> <0x-address>")]
    [InlineData("/remove ethereum", "Usage: /remove <network> <0x-address>")]
    public void MissingParametersReturnUsage(string text, string expectedError)
    {
        BotCommandParseResult result = BotCommandParser.Parse(text);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public void UnknownCommandReturnsHelpGuidance()
    {
        BotCommandParseResult result = BotCommandParser.Parse("/unknown");

        Assert.False(result.IsSuccess);
        Assert.Contains("/help", result.Error, StringComparison.Ordinal);
    }
}
