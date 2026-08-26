namespace EVMTracker.TelegramGateway.Application.Delivery;

public enum TelegramDeliveryStatus
{
    Pending = 0,
    RetryScheduled = 1,
    Delivered = 2,
    PermanentlyFailed = 3,
}
