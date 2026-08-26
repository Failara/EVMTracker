namespace EVMTracker.TelegramGateway.Application.Commands;

public sealed class BotCommandParser
{
    public static BotCommandParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BotCommandParseResult.Failure("Send /help to see the available commands.");
        }

        string[] parts = text.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string commandName = NormalizeCommandName(parts[0]);

        return commandName switch
        {
            "/start" when parts.Length == 1 =>
                BotCommandParseResult.Success(new BotCommand.Start()),
            "/help" when parts.Length == 1 =>
                BotCommandParseResult.Success(new BotCommand.Help()),
            "/add" when parts.Length >= 3 =>
                BotCommandParseResult.Success(
                    new BotCommand.Add(
                        parts[1],
                        parts[2],
                        parts.Length > 3 ? string.Join(' ', parts[3..]) : null)),
            "/list" when parts.Length == 1 =>
                BotCommandParseResult.Success(new BotCommand.List()),
            "/remove" when parts.Length == 3 =>
                BotCommandParseResult.Success(new BotCommand.Remove(parts[1], parts[2])),
            "/status" when parts.Length == 1 =>
                BotCommandParseResult.Success(new BotCommand.Status()),
            "/add" => BotCommandParseResult.Failure(
                "Usage: /add <network> <0x-address> [label]"),
            "/remove" => BotCommandParseResult.Failure(
                "Usage: /remove <network> <0x-address>"),
            "/start" or "/help" or "/list" or "/status" =>
                BotCommandParseResult.Failure($"Usage: {commandName}"),
            _ => BotCommandParseResult.Failure(
                "Unknown command. Send /help to see the available commands."),
        };
    }

    private static string NormalizeCommandName(string command)
    {
        int botNameSeparator = command.IndexOf('@', StringComparison.Ordinal);
        string commandName = botNameSeparator >= 0
            ? command[..botNameSeparator]
            : command;

        return commandName.ToLowerInvariant();
    }
}
