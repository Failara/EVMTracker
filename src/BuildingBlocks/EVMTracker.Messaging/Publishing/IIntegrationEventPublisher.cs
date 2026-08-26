using EVMTracker.Contracts;

namespace EVMTracker.Messaging.Publishing;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(
        IntegrationEventEnvelope envelope,
        CancellationToken cancellationToken);
}
