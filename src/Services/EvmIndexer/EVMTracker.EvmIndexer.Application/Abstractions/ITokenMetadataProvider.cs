using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Transfers;

namespace EVMTracker.EvmIndexer.Application.Abstractions;

public interface ITokenMetadataProvider
{
    Task<TokenMetadata> GetAsync(
        long chainId,
        EvmAddress tokenContract,
        CancellationToken cancellationToken);
}
