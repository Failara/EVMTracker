namespace EVMTracker.TelegramGateway.Application.Commands;

public sealed class BotCommandParseResult
{
    private BotCommandParseResult(BotCommand? command, string? error)
    {
        Command = command;
        Error = error;
    }

    public bool IsSuccess => Command is not null;

    public BotCommand? Command { get; }

    public string? Error { get; }

    public static BotCommandParseResult Success(BotCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new BotCommandParseResult(command, null);
    }

    public static BotCommandParseResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new BotCommandParseResult(null, error);
    }
}
