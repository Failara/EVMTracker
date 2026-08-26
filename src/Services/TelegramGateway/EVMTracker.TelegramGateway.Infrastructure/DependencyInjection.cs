using EVMTracker.Messaging;
using EVMTracker.TelegramGateway.Application.Abstractions;
using EVMTracker.TelegramGateway.Application.Commands;
using EVMTracker.TelegramGateway.Application.Delivery;
using EVMTracker.TelegramGateway.Infrastructure.Chains;
using EVMTracker.TelegramGateway.Infrastructure.Configuration;
using EVMTracker.TelegramGateway.Infrastructure.Messaging;
using EVMTracker.TelegramGateway.Infrastructure.Persistence;
using EVMTracker.TelegramGateway.Infrastructure.Telegram;
using EVMTracker.TelegramGateway.Infrastructure.Watchlists;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace EVMTracker.TelegramGateway.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTelegramGatewayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string connectionString = configuration.GetConnectionString("TelegramGateway")
            ?? throw new InvalidOperationException(
                "The ConnectionStrings:TelegramGateway setting is required.");

        services.AddOptions<TelegramBotOptions>()
            .Bind(configuration.GetSection(TelegramBotOptions.SectionName))
            .Validate(
                options => IsValidBotToken(options.BotToken),
                "TelegramBot:BotToken must contain a valid BotFather token.")
            .Validate(
                options => options.PollingTimeoutSeconds is >= 1 and <= 50,
                "TelegramBot:PollingTimeoutSeconds must be between 1 and 50.")
            .Validate(
                options => options.ErrorRetryDelay > TimeSpan.Zero,
                "TelegramBot:ErrorRetryDelay must be positive.")
            .ValidateOnStart();

        services.AddOptions<WatchlistApiOptions>()
            .Bind(configuration.GetSection(WatchlistApiOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(
                    options.BaseUrl,
                    UriKind.Absolute,
                    out Uri? baseUri) &&
                    (baseUri.Scheme == Uri.UriSchemeHttp ||
                        baseUri.Scheme == Uri.UriSchemeHttps),
                "WatchlistApi:BaseUrl must be an absolute HTTP or HTTPS URL.")
            .Validate(
                options => options.RequestTimeout > TimeSpan.Zero,
                "WatchlistApi:RequestTimeout must be positive.")
            .ValidateOnStart();

        services.AddOptions<SupportedNetworksOptions>()
            .Bind(configuration.GetSection(SupportedNetworksOptions.SectionName))
            .Validate(AreValidNetworks, "SupportedNetworks contains invalid or duplicate entries.")
            .ValidateOnStart();
        services.AddOptions<TelegramDeliveryOptions>()
            .Bind(configuration.GetSection(TelegramDeliveryOptions.SectionName))
            .Validate(IsValid, "TelegramDelivery configuration is invalid.")
            .ValidateOnStart();

        services.AddDbContext<TelegramGatewayDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    "telegram_gateway")));

        services.AddSingleton<IChainCatalog>(serviceProvider =>
        {
            SupportedNetworksOptions networkOptions = serviceProvider
                .GetRequiredService<IOptions<SupportedNetworksOptions>>()
                .Value;
            return new ConfiguredChainCatalog(
                networkOptions.Networks.Select(
                    network => new ChainNetwork(
                        network.Name,
                        network.DisplayName,
                        network.ChainId)));
        });
        services.AddSingleton<TelegramCommandService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITelegramDeliveryRateLimiter, TelegramDeliveryRateLimiter>();
        services.AddSingleton<IDeliveryDelay, SystemDeliveryDelay>();
        services.AddScoped<ITelegramDeliveryStore, TelegramDeliveryStore>();
        services.AddScoped<TelegramDeliveryProcessor>();

        services.AddHttpClient<IWatchlistClient, WatchlistApiClient>(
            (serviceProvider, client) =>
            {
                WatchlistApiOptions apiOptions = serviceProvider
                    .GetRequiredService<IOptions<WatchlistApiOptions>>()
                    .Value;
                client.BaseAddress = new Uri(
                    apiOptions.BaseUrl.EndsWith('/')
                        ? apiOptions.BaseUrl
                        : apiOptions.BaseUrl + "/",
                    UriKind.Absolute);
                client.Timeout = apiOptions.RequestTimeout;
            });

        services.AddSingleton<ITelegramBotClient>(serviceProvider =>
        {
            TelegramBotOptions botOptions = serviceProvider
                .GetRequiredService<IOptions<TelegramBotOptions>>()
                .Value;
            return new TelegramBotClient(botOptions.BotToken);
        });
        services.AddSingleton<ITelegramMessageSender, TelegramMessageSender>();
        services.AddSingleton(serviceProvider =>
        {
            TelegramDeliveryOptions deliveryOptions = serviceProvider
                .GetRequiredService<IOptions<TelegramDeliveryOptions>>()
                .Value;
            return new TelegramDeliveryPolicy(
                deliveryOptions.MaximumAttempts,
                deliveryOptions.InitialRetryDelay,
                deliveryOptions.MaximumRetryDelay,
                deliveryOptions.JitterFactor);
        });
        services.AddRabbitMqMessaging(
            configuration,
            registration => registration.AddConsumer<TelegramDeliveryConsumer>(),
            (broker, context, _) =>
            {
                TelegramDeliveryOptions deliveryOptions = context.GetRequiredService<
                    IOptions<TelegramDeliveryOptions>>().Value;
                broker.ReceiveEndpoint(
                    deliveryOptions.QueueName,
                    endpoint =>
                    {
                        endpoint.Durable = true;
                        endpoint.AutoDelete = false;
                        endpoint.PrefetchCount = deliveryOptions.PrefetchCount;
                        endpoint.ConcurrentMessageLimit =
                            deliveryOptions.ConcurrentMessageLimit;
                        endpoint.ConfigureConsumer<TelegramDeliveryConsumer>(context);
                    });
            });
        services.AddHostedService<TelegramPollingWorker>();

        return services;
    }

    private static bool IsValidBotToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        int separator = token.IndexOf(':', StringComparison.Ordinal);
        return separator > 0 && separator < token.Length - 1;
    }

    private static bool AreValidNetworks(SupportedNetworksOptions options)
    {
        if (options.Networks.Count == 0)
        {
            return false;
        }

        bool valuesAreValid = options.Networks.All(
            network => !string.IsNullOrWhiteSpace(network.Name) &&
                !string.IsNullOrWhiteSpace(network.DisplayName) &&
                network.ChainId > 0);
        bool namesAreUnique = options.Networks
            .Select(network => network.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == options.Networks.Count;
        bool chainIdsAreUnique = options.Networks
            .Select(network => network.ChainId)
            .Distinct()
            .Count() == options.Networks.Count;

        return valuesAreValid && namesAreUnique && chainIdsAreUnique;
    }

    private static bool IsValid(TelegramDeliveryOptions options) =>
        !string.IsNullOrWhiteSpace(options.QueueName) &&
        options.PrefetchCount > 0 &&
        options.ConcurrentMessageLimit > 0 &&
        options.MaximumAttempts > 0 &&
        options.InitialRetryDelay > TimeSpan.Zero &&
        options.MaximumRetryDelay >= options.InitialRetryDelay &&
        options.JitterFactor is >= 0 and <= 1 &&
        options.GlobalMessagesPerSecond > 0 &&
        options.PerChatMessagesPerSecond > 0;
}
