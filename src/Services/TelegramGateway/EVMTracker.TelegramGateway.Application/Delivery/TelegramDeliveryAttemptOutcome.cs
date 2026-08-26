namespace EVMTracker.TelegramGateway.Application.Delivery;

public enum TelegramDeliveryAttemptOutcome
{
    Delivered = 0,
    TransientFailure = 1,
    PermanentFailure = 2,
}
