namespace EVMTracker.Contracts;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    string EventType { get; }

    int EventVersion { get; }

    DateTimeOffset OccurredAtUtc { get; }
}
