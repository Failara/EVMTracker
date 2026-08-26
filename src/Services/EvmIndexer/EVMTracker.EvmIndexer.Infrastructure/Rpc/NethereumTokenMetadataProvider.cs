using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Transfers;
using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Web3;

namespace EVMTracker.EvmIndexer.Infrastructure.Rpc;

internal sealed class NethereumTokenMetadataProvider : ITokenMetadataProvider
{
    private const string MetadataAbi = """
        [
          {"constant":true,"inputs":[],"name":"symbol","outputs":[{"name":"","type":"string"}],"type":"function"},
          {"constant":true,"inputs":[],"name":"name","outputs":[{"name":"","type":"string"}],"type":"function"},
          {"constant":true,"inputs":[],"name":"decimals","outputs":[{"name":"","type":"uint8"}],"type":"function"}
        ]
        """;

    private readonly ConcurrentDictionary<(long ChainId, string Address), Lazy<Task<TokenMetadata>>>
        cache = new();
    private readonly Dictionary<long, Web3> clients;
    private readonly TimeSpan requestTimeout;

    public NethereumTokenMetadataProvider(IOptions<EvmIndexerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        requestTimeout = options.Value.RpcRequestTimeout;
        clients = options.Value.Networks.ToDictionary(
            network => network.ChainId,
            network => new Web3(network.RpcUrl));
    }

    public async Task<TokenMetadata> GetAsync(
        long chainId,
        EvmAddress tokenContract,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenContract);
        Lazy<Task<TokenMetadata>> metadata = cache.GetOrAdd(
            (chainId, tokenContract.Value),
            key => new Lazy<Task<TokenMetadata>>(
                () => LoadMetadataAsync(key.ChainId, key.Address),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return await metadata.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Non-standard ERC-20 metadata must degrade to a safe fallback without stopping indexing.")]
    private async Task<TokenMetadata> LoadMetadataAsync(long chainId, string tokenContract)
    {
        try
        {
            Web3 web3 = clients.TryGetValue(chainId, out Web3? client)
                ? client
                : throw new InvalidOperationException(
                    $"No RPC client is configured for chain {chainId}.");
            Contract contract = web3.Eth.GetContract(MetadataAbi, tokenContract);
            string symbol = await contract.GetFunction("symbol")
                .CallAsync<string>()
                .WaitAsync(requestTimeout).ConfigureAwait(false);
            string name = await contract.GetFunction("name")
                .CallAsync<string>()
                .WaitAsync(requestTimeout).ConfigureAwait(false);
            BigInteger decimalsValue = await contract.GetFunction("decimals")
                .CallAsync<BigInteger>()
                .WaitAsync(requestTimeout).ConfigureAwait(false);

            string normalizedSymbol = string.IsNullOrWhiteSpace(symbol)
                ? TokenMetadata.Unknown(tokenContract).Symbol
                : symbol.Trim()[..Math.Min(symbol.Trim().Length, 64)];
            string? normalizedName = string.IsNullOrWhiteSpace(name)
                ? null
                : name.Trim()[..Math.Min(name.Trim().Length, 256)];
            int? decimals = decimalsValue >= BigInteger.Zero &&
                decimalsValue <= new BigInteger(255)
                ? (int)decimalsValue
                : null;
            return new TokenMetadata(normalizedSymbol, normalizedName, decimals);
        }
        catch (Exception)
        {
            return TokenMetadata.Unknown(tokenContract);
        }
    }
}
