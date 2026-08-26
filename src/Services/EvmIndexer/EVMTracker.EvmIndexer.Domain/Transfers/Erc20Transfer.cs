using System.Numerics;

namespace EVMTracker.EvmIndexer.Domain.Transfers;

public sealed record Erc20Transfer(
    long ChainId,
    EvmAddress TokenContract,
    EvmAddress FromAddress,
    EvmAddress ToAddress,
    BigInteger RawValue,
    Hash256 TransactionHash,
    long BlockNumber,
    Hash256 BlockHash,
    long LogIndex)
{
    public TransferIdentity Identity => new(ChainId, TransactionHash, LogIndex);
}
