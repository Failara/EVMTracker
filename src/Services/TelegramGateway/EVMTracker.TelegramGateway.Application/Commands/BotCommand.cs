namespace EVMTracker.TelegramGateway.Application.Commands;

public abstract record BotCommand
{
    private BotCommand()
    {
    }

    public sealed record Start : BotCommand;

    public sealed record Help : BotCommand;

    public sealed record Add(string Network, string Address, string? Label) : BotCommand;

    public sealed record List : BotCommand;

    public sealed record Remove(string Network, string Address) : BotCommand;

    public sealed record Status : BotCommand;
}
