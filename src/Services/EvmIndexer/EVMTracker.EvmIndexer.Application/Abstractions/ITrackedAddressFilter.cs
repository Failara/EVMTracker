namespace EVMTracker.EvmIndexer.Application.Abstractions;

public interface ITrackedAddressFilter
{
    Task<IReadOnlyCollection<string>> ListAsync(
        long chainId,
        CancellationToken cancellationToken);
}
