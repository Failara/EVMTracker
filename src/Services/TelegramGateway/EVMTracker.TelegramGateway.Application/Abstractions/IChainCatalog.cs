namespace EVMTracker.TelegramGateway.Application.Abstractions;

public interface IChainCatalog
{
    IReadOnlyCollection<ChainNetwork> Networks { get; }

    ChainNetwork? Find(string name);
}
