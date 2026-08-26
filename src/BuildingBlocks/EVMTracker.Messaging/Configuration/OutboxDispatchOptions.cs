namespace EVMTracker.Messaging.Configuration;

public sealed class OutboxDispatchOptions
{
    public const string SectionName = "OutboxDispatch";

    public int BatchSize { get; init; } = 100;

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    public int MaximumDeliveryAttempts { get; init; } = 10;

    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromMinutes(5);
}
