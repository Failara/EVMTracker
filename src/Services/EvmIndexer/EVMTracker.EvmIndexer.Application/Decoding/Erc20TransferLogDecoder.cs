using System.Buffers;
using System.Globalization;
using System.Numerics;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Transfers;

namespace EVMTracker.EvmIndexer.Application.Decoding;

public sealed class Erc20TransferLogDecoder
{
    public const string TransferTopic =
        "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";
    private static readonly SearchValues<char> HexCharacters =
        SearchValues.Create("0123456789abcdefABCDEF");

    public static TransferLogDecodeResult Decode(long chainId, RawEvmLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (chainId <= 0 || log.IsRemoved)
        {
            return TransferLogDecodeResult.Failure("The log is removed or has an invalid chain ID.");
        }

        if (log.Topics.Count < 3 ||
            !string.Equals(log.Topics[0], TransferTopic, StringComparison.OrdinalIgnoreCase))
        {
            return TransferLogDecodeResult.Failure("The log is not an ERC-20 Transfer event.");
        }

        if (!TryDecodeTopicAddress(log.Topics[1], out EvmAddress? fromAddress) ||
            !TryDecodeTopicAddress(log.Topics[2], out EvmAddress? toAddress) ||
            !EvmAddress.TryCreate(log.ContractAddress, out EvmAddress? tokenContract) ||
            !Hash256.TryCreate(log.TransactionHash, out Hash256? transactionHash) ||
            !Hash256.TryCreate(log.BlockHash, out Hash256? blockHash) ||
            !TryDecodeValue(log.Data, out BigInteger rawValue) ||
            log.BlockNumber < 0 ||
            log.LogIndex < 0)
        {
            return TransferLogDecodeResult.Failure("The Transfer log contains malformed fields.");
        }

        return TransferLogDecodeResult.Success(
            new Erc20Transfer(
                chainId,
                tokenContract!,
                fromAddress!,
                toAddress!,
                rawValue,
                transactionHash!,
                log.BlockNumber,
                blockHash!,
                log.LogIndex));
    }

    private static bool TryDecodeTopicAddress(string? topic, out EvmAddress? address)
    {
        address = null;
        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        string candidate = topic.Trim();
        if (candidate.Length != 66 ||
            !candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return EvmAddress.TryCreate("0x" + candidate[^40..], out address);
    }

    private static bool TryDecodeValue(string? data, out BigInteger value)
    {
        value = BigInteger.Zero;
        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        string candidate = data.Trim();
        if (candidate.Length != 66 ||
            !candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            candidate.AsSpan(2).ContainsAnyExcept(HexCharacters))
        {
            return false;
        }

        return BigInteger.TryParse(
            "0" + candidate[2..],
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture,
            out value);
    }
}
