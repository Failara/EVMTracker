namespace EVMTracker.TelegramGateway.Infrastructure.Configuration;

public sealed class WatchlistApiOptions
{
    public const string SectionName = "WatchlistApi";

    public string BaseUrl { get; set; } = string.Empty;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
