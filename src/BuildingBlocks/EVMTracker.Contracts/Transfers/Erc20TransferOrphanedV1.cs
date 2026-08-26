namespace EVMTracker.Contracts.Transfers;

public sealed record Erc20TransferOrphanedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    long ChainId,
    string TransactionHash,
    long LogIndex,
    string FormerBlockHash) : ITransferIntegrationEvent
{
    public string EventType => TransferEventTypes.Erc20TransferOrphanedV1;

    public int EventVersion => 1;
}
