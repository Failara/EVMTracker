namespace EVMTracker.TelegramGateway.Application.Delivery;

public interface IDeliveryDelay
{
    Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
}
