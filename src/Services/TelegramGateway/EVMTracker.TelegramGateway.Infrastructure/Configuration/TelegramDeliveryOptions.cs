namespace EVMTracker.TelegramGateway.Infrastructure.Configuration;

public sealed class TelegramDeliveryOptions
{
    public const string SectionName = "TelegramDelivery";

    public string QueueName { get; init; } = "telegram-delivery";

    public ushort PrefetchCount { get; init; } = 32;

    public int ConcurrentMessageLimit { get; init; } = 8;

    public int MaximumAttempts { get; init; } = 5;

    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromMinutes(1);

    public double JitterFactor { get; init; } = 0.2;

    public int GlobalMessagesPerSecond { get; init; } = 30;

    public int PerChatMessagesPerSecond { get; init; } = 1;
}
