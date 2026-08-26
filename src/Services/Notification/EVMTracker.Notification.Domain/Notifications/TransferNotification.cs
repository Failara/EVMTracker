using System.Security.Cryptography;
using System.Text;

namespace EVMTracker.Notification.Domain.Notifications;

public sealed class TransferNotification
{
    private TransferNotification(
        Guid id,
        Guid subscriptionId,
        Guid sourceEventId,
        long telegramUserId,
        long telegramChatId,
        long chainId,
        string networkName,
        string monitoredAddress,
        string? label,
        TransferDirection direction,
        string tokenContract,
        string tokenSymbol,
        string? tokenName,
        int? tokenDecimals,
        string rawValue,
        string displayAmount,
        string fromAddress,
        string toAddress,
        string transactionHash,
        long blockNumber,
        string blockHash,
        long logIndex,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        SubscriptionId = subscriptionId;
        SourceEventId = sourceEventId;
        TelegramUserId = telegramUserId;
        TelegramChatId = telegramChatId;
        ChainId = chainId;
        NetworkName = networkName;
        MonitoredAddress = monitoredAddress;
        Label = label;
        Direction = direction;
        TokenContract = tokenContract;
        TokenSymbol = tokenSymbol;
        TokenName = tokenName;
        TokenDecimals = tokenDecimals;
        RawValue = rawValue;
        DisplayAmount = displayAmount;
        FromAddress = fromAddress;
        ToAddress = toAddress;
        TransactionHash = transactionHash;
        BlockNumber = blockNumber;
        BlockHash = blockHash;
        LogIndex = logIndex;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public Guid SubscriptionId { get; }

    public Guid SourceEventId { get; }

    public long TelegramUserId { get; }

    public long TelegramChatId { get; }

    public long ChainId { get; }

    public string NetworkName { get; }

    public string MonitoredAddress { get; }

    public string? Label { get; }

    public TransferDirection Direction { get; }

    public string TokenContract { get; }

    public string TokenSymbol { get; }

    public string? TokenName { get; }

    public int? TokenDecimals { get; }

    public string RawValue { get; }

    public string DisplayAmount { get; }

    public string FromAddress { get; }

    public string ToAddress { get; }

    public string TransactionHash { get; }

    public long BlockNumber { get; }

    public string BlockHash { get; }

    public long LogIndex { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static TransferNotification Create(
        Guid subscriptionId,
        Guid sourceEventId,
        long telegramUserId,
        long telegramChatId,
        long chainId,
        string networkName,
        string monitoredAddress,
        string? label,
        TransferDirection direction,
        string tokenContract,
        string tokenSymbol,
        string? tokenName,
        int? tokenDecimals,
        string rawValue,
        string fromAddress,
        string toAddress,
        string transactionHash,
        long blockNumber,
        string blockHash,
        long logIndex,
        DateTimeOffset createdAtUtc)
    {
        if (subscriptionId == Guid.Empty || sourceEventId == Guid.Empty)
        {
            throw new ArgumentException("Subscription and source event identifiers are required.");
        }

        if (telegramUserId <= 0 || chainId <= 0 || blockNumber < 0 || logIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(telegramUserId),
                "User, chain, block, and log identifiers must be valid.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(networkName);
        ArgumentException.ThrowIfNullOrWhiteSpace(monitoredAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenContract);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenSymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(toAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockHash);

        string normalizedTransactionHash = transactionHash.ToLowerInvariant();
        Guid id = CreateStableId(subscriptionId, chainId, normalizedTransactionHash, logIndex);
        return new TransferNotification(
            id,
            subscriptionId,
            sourceEventId,
            telegramUserId,
            telegramChatId,
            chainId,
            networkName,
            monitoredAddress.ToLowerInvariant(),
            label,
            direction,
            tokenContract.ToLowerInvariant(),
            tokenSymbol,
            tokenName,
            tokenDecimals,
            rawValue,
            TokenAmountFormatter.Format(rawValue, tokenDecimals),
            fromAddress.ToLowerInvariant(),
            toAddress.ToLowerInvariant(),
            normalizedTransactionHash,
            blockNumber,
            blockHash.ToLowerInvariant(),
            logIndex,
            createdAtUtc);
    }

    private static Guid CreateStableId(
        Guid subscriptionId,
        long chainId,
        string transactionHash,
        long logIndex)
    {
        byte[] identity = Encoding.UTF8.GetBytes(
            $"{subscriptionId:N}:{chainId}:{transactionHash}:{logIndex}");
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(identity, hash);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash[..16]);
    }
}
