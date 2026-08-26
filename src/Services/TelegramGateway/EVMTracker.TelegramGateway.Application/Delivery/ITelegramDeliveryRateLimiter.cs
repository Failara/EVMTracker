namespace EVMTracker.TelegramGateway.Application.Delivery;

public interface ITelegramDeliveryRateLimiter
{
    Task WaitAsync(long chatId, CancellationToken cancellationToken);
}
