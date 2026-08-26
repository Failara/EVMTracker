using EVMTracker.EvmIndexer.Domain.Transfers;

namespace EVMTracker.EvmIndexer.Application.Decoding;

public sealed record TransferLogDecodeResult(Erc20Transfer? Transfer, string? Error)
{
    public bool IsSuccess => Transfer is not null;

    public static TransferLogDecodeResult Success(Erc20Transfer transfer) => new(transfer, null);

    public static TransferLogDecodeResult Failure(string error) => new(null, error);
}
