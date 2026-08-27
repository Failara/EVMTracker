using System.Security.Cryptography;
using System.Text;
using EVMTracker.EvmIndexer.Domain.Transfers;

namespace EVMTracker.EvmIndexer.Application.Indexing;

public static class TransferEventIdentity
{
    public static Guid CreateTransferId(
        long chainId,
        string transactionHash,
        long logIndex) =>
        Create($"transfer:{chainId}:{transactionHash.ToLowerInvariant()}:{logIndex}");

    public static Guid CreateObservedEventId(Erc20Transfer transfer) =>
        Create(
            $"observed:{transfer.ChainId}:{transfer.TransactionHash.Value}:{transfer.LogIndex}:{transfer.BlockHash.Value}");

    public static Guid CreateOrphanedEventId(
        long chainId,
        string transactionHash,
        long logIndex,
        string formerBlockHash) =>
        Create(
            $"orphaned:{chainId}:{transactionHash.ToLowerInvariant()}:{logIndex}:{formerBlockHash.ToLowerInvariant()}");

    private static Guid Create(string identity)
    {
        byte[] value = Encoding.UTF8.GetBytes(identity);
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(value, hash);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash[..16]);
    }
}
