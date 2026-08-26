namespace EVMTracker.TelegramGateway.Application.Delivery;

public sealed record TelegramSendResult(
    bool Succeeded,
    bool IsPermanentFailure,
    int? TelegramMessageId,
    string? Error,
    TimeSpan? RetryAfter)
{
    public static TelegramSendResult Success(int telegramMessageId) =>
        new(true, false, telegramMessageId, null, null);

    public static TelegramSendResult TransientFailure(
        string error,
        TimeSpan? retryAfter = null) =>
        new(false, false, null, error, retryAfter);

    public static TelegramSendResult PermanentFailure(string error) =>
        new(false, true, null, error, null);
}
