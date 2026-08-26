using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Application.Indexing;
using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using EVMTracker.EvmIndexer.Infrastructure.Indexing;
using EVMTracker.EvmIndexer.Infrastructure.Persistence;
using EVMTracker.EvmIndexer.Infrastructure.Rpc;
using EVMTracker.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EVMTracker.EvmIndexer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEvmIndexerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddOptions<EvmIndexerOptions>()
            .Bind(configuration.GetSection(EvmIndexerOptions.SectionName))
            .Validate(AreValidOptions, "EvmIndexer configuration contains invalid network settings.")
            .ValidateOnStart();

        services.AddDbContext<IndexerDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                postgresOptions => postgresOptions.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    "evm_indexer")));
        services.AddScoped<IIndexerStateStore, IndexerStateStore>();
        services.AddScoped<IOutboxBatchStore, IndexerOutboxBatchStore>();
        services.AddSingleton<IEvmLogSource, NethereumEvmLogSource>();
        services.AddSingleton<ITokenMetadataProvider, NethereumTokenMetadataProvider>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<EvmIndexerProcessor>();
        services.AddHostedService<EvmIndexerWorker>();

        return services;
    }

    private static bool AreValidOptions(EvmIndexerOptions options)
    {
        if (options.RpcRequestTimeout <= TimeSpan.Zero || options.Networks.Count == 0)
        {
            return false;
        }

        bool entriesAreValid = options.Networks.All(
            network => !string.IsNullOrWhiteSpace(network.Name) &&
                network.ChainId > 0 &&
                Uri.TryCreate(network.RpcUrl, UriKind.Absolute, out Uri? rpcUri) &&
                (rpcUri.Scheme == Uri.UriSchemeHttp || rpcUri.Scheme == Uri.UriSchemeHttps) &&
                network.StartBlock >= 0 &&
                network.ConfirmationDepth >= 0 &&
                network.MinimumBlockRange > 0 &&
                network.InitialBlockRange >= network.MinimumBlockRange &&
                network.MaximumBlockRange >= network.InitialBlockRange &&
                network.PollInterval > TimeSpan.Zero);
        bool chainIdsAreUnique = options.Networks
            .Select(network => network.ChainId)
            .Distinct()
            .Count() == options.Networks.Count;
        bool namesAreUnique = options.Networks
            .Select(network => network.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == options.Networks.Count;

        return entriesAreValid && chainIdsAreUnique && namesAreUnique;
    }
}
