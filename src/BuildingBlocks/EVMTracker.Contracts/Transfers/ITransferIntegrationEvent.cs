namespace EVMTracker.Contracts.Transfers;

public interface ITransferIntegrationEvent
{
    Guid EventId { get; }

    string EventType { get; }

    DateTimeOffset OccurredAtUtc { get; }
}
