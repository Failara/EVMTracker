using EVMTracker.TelegramGateway.Application.Delivery;

namespace EVMTracker.TelegramGateway.Infrastructure.Persistence.Entities;

internal sealed class TelegramDeliveryAttemptRecord
{
    public Guid EventId { get; set; }

    public int AttemptNumber { get; set; }

    public DateTimeOffset AttemptedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public TelegramDeliveryAttemptOutcome? Outcome { get; set; }

    public int? TelegramMessageId { get; set; }

    public string? Error { get; set; }

    public long? RetryDelayMilliseconds { get; set; }
}
