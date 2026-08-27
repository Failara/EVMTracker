using EVMTracker.EvmIndexer.Application.Models;

namespace EVMTracker.EvmIndexer.Application.Abstractions;

public interface IEvmLogSource
{
    Task<long> GetHeadBlockNumberAsync(long chainId, CancellationToken cancellationToken);

    Task<string> GetBlockHashAsync(
        long chainId,
        long blockNumber,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RawEvmLog>> GetTransferLogsAsync(
        long chainId,
        long fromBlock,
        long toBlock,
        CancellationToken cancellationToken);
}
