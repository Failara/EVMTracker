using EVMTracker.TelegramGateway.Application.Delivery;

namespace EVMTracker.TelegramGateway.Infrastructure.Telegram;

internal sealed class SystemDeliveryDelay(TimeProvider timeProvider) : IDeliveryDelay
{
    public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, timeProvider, cancellationToken);
}
