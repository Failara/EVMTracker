using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Application.Indexing;
using EVMTracker.EvmIndexer.Application.Watchlists;
using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using EVMTracker.EvmIndexer.Infrastructure.Indexing;
using EVMTracker.EvmIndexer.Infrastructure.Messaging;
using EVMTracker.EvmIndexer.Infrastructure.Persistence;
using EVMTracker.EvmIndexer.Infrastructure.Rpc;
using EVMTracker.Messaging;
using MassTransit;
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
        services.AddOptions<EvmIndexerMessagingOptions>()
            .Bind(configuration.GetSection(EvmIndexerMessagingOptions.SectionName))
            .Validate(IsValid, "EvmIndexerMessaging configuration is invalid.")
            .ValidateOnStart();
        services.AddOptions<IndexerDataRetentionOptions>()
            .Bind(configuration.GetSection(IndexerDataRetentionOptions.SectionName))
            .Validate(IsValid, "IndexerDataRetention configuration is invalid.")
            .ValidateOnStart();
        services.AddOptions<IndexerWatchlistBootstrapOptions>()
            .Bind(configuration.GetSection(IndexerWatchlistBootstrapOptions.SectionName))
            .Validate(IsValid, "IndexerWatchlistBootstrap configuration is invalid.")
            .ValidateOnStart();
        services.AddScoped<IIndexerStateStore, IndexerStateStore>();
        services.AddScoped<IOutboxBatchStore, IndexerOutboxBatchStore>();
        services.AddScoped<IndexerWatchlistProjectionStore>();
        services.AddScoped<IIndexerWatchlistProjectionStore>(provider =>
            provider.GetRequiredService<IndexerWatchlistProjectionStore>());
        services.AddScoped<ITrackedAddressFilter>(provider =>
            provider.GetRequiredService<IndexerWatchlistProjectionStore>());
        services.AddScoped<IndexerWatchlistProjectionProjector>();
        services.AddScoped<IndexerDataRetentionService>();
        services.AddSingleton<IEvmLogSource, NethereumEvmLogSource>();
        services.AddSingleton<ITokenMetadataProvider, NethereumTokenMetadataProvider>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<EvmIndexerProcessor>();
        services.AddHostedService<EvmIndexerWorker>();
        services.AddHostedService<IndexerDataRetentionWorker>();
        services.AddRabbitMqMessaging(
            configuration,
            registration => registration.AddConsumer<IndexerWatchlistProjectionConsumer>(),
            (broker, context, _) =>
            {
                EvmIndexerMessagingOptions messagingOptions = context.GetRequiredService<
                    Microsoft.Extensions.Options.IOptions<EvmIndexerMessagingOptions>>().Value;
                broker.ReceiveEndpoint(
                    messagingOptions.WatchlistProjectionQueue,
                    endpoint =>
                    {
                        endpoint.Durable = true;
                        endpoint.AutoDelete = false;
                        endpoint.PrefetchCount = messagingOptions.PrefetchCount;
                        endpoint.ConcurrentMessageLimit = 1;
                        endpoint.UseMessageRetry(retry =>
                            retry.Intervals(messagingOptions.RetryIntervals));
                        endpoint.ConfigureConsumer<IndexerWatchlistProjectionConsumer>(context);
                    });
            });
        services.AddHttpClient(
            "watchlist-bootstrap",
            (provider, client) =>
            {
                IndexerWatchlistBootstrapOptions bootstrap = provider.GetRequiredService<
                    Microsoft.Extensions.Options.IOptions<IndexerWatchlistBootstrapOptions>>()
                    .Value;
                client.BaseAddress = bootstrap.BaseUrl;
            });
        services.AddHostedService<IndexerWatchlistBootstrapWorker>();

        return services;
    }

    private static bool IsValid(EvmIndexerMessagingOptions options) =>
        !string.IsNullOrWhiteSpace(options.WatchlistProjectionQueue) &&
        options.PrefetchCount > 0 &&
        options.RetryIntervals.Length > 0 &&
        options.RetryIntervals.All(interval => interval > TimeSpan.Zero);

    private static bool IsValid(IndexerDataRetentionOptions options) =>
        options.CleanupInterval > TimeSpan.Zero &&
        options.PublishedOutboxRetention > TimeSpan.Zero &&
        options.TransferHistoryRetention > TimeSpan.Zero &&
        options.WatchlistInboxRetention > TimeSpan.Zero &&
        options.BatchSize > 0;

    private static bool IsValid(IndexerWatchlistBootstrapOptions options) =>
        options.BaseUrl.IsAbsoluteUri &&
        options.BaseUrl.Scheme is "http" or "https" &&
        options.RetryInterval > TimeSpan.Zero;

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
                network.ReorganizationWindow >= 2 &&
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
