namespace EVMTracker.TelegramGateway.Infrastructure.Configuration;

public sealed class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";

    public string BotToken { get; set; } = string.Empty;

    public int PollingTimeoutSeconds { get; set; } = 30;

    public TimeSpan ErrorRetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}
