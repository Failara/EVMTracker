namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;

internal sealed class ProcessedTransferRecord
{
    public Guid Id { get; set; }

    public long ChainId { get; set; }

    public string TransactionHash { get; set; } = string.Empty;

    public long LogIndex { get; set; }

    public long BlockNumber { get; set; }

    public string BlockHash { get; set; } = string.Empty;

    public string TokenContract { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string ToAddress { get; set; } = string.Empty;

    public string RawValue { get; set; } = string.Empty;

    public string TokenSymbol { get; set; } = string.Empty;

    public string? TokenName { get; set; }

    public int? TokenDecimals { get; set; }

    public DateTimeOffset ObservedAtUtc { get; set; }
}
