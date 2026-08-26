namespace EVMTracker.TelegramGateway.Application.Delivery;

public static class TelegramRetryDelayCalculator
{
    public static TimeSpan Calculate(
        Guid eventId,
        int attemptNumber,
        TimeSpan? retryAfter,
        TelegramDeliveryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (retryAfter is { } serverDelay)
        {
            return serverDelay;
        }

        double multiplier = Math.Pow(2, Math.Max(0, attemptNumber - 1));
        double baseMilliseconds = Math.Min(
            policy.InitialRetryDelay.TotalMilliseconds * multiplier,
            policy.MaximumRetryDelay.TotalMilliseconds);
        uint seed = BitConverter.ToUInt32(eventId.ToByteArray(), 0) ^ (uint)attemptNumber;
        double jitterRatio = seed / (double)uint.MaxValue * policy.JitterFactor;
        double milliseconds = Math.Min(
            baseMilliseconds * (1 + jitterRatio),
            policy.MaximumRetryDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
