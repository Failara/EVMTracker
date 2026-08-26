namespace EVMTracker.TelegramGateway.Application.Delivery;

public sealed record TelegramDeliveryPolicy(
    int MaximumAttempts,
    TimeSpan InitialRetryDelay,
    TimeSpan MaximumRetryDelay,
    double JitterFactor);
