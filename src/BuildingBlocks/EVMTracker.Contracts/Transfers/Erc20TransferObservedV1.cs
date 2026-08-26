namespace EVMTracker.Contracts.Transfers;

public sealed record Erc20TransferObservedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    long ChainId,
    string TokenContract,
    string FromAddress,
    string ToAddress,
    string RawValue,
    string TokenSymbol,
    string? TokenName,
    int? TokenDecimals,
    string TransactionHash,
    long BlockNumber,
    string BlockHash,
    long LogIndex) : ITransferIntegrationEvent
{
    public string EventType => TransferEventTypes.Erc20TransferObservedV1;
}
