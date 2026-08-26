using EVMTracker.Messaging.Configuration;
using EVMTracker.Messaging.Publishing;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EVMTracker.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? registerConsumers = null,
        Action<IRabbitMqBusFactoryConfigurator, IBusRegistrationContext, RabbitMqOptions>?
            configureEndpoints = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(IsValid, "RabbitMq configuration is invalid.")
            .ValidateOnStart();
        services.AddMassTransit(registration =>
        {
            registration.SetKebabCaseEndpointNameFormatter();
            registerConsumers?.Invoke(registration);
            registration.UsingRabbitMq(
                (context, broker) =>
                {
                    RabbitMqOptions options = context.GetRequiredService<
                        Microsoft.Extensions.Options.IOptions<RabbitMqOptions>>().Value;
                    broker.Host(
                        options.Host,
                        options.Port,
                        options.VirtualHost,
                        host =>
                        {
                            host.Username(options.Username);
                            host.Password(options.Password);
                        });
                    configureEndpoints?.Invoke(broker, context, options);
                });
        });
        services.TryAddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        return services;
    }

    public static IServiceCollection AddOutboxDispatcher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OutboxDispatchOptions>()
            .Bind(configuration.GetSection(OutboxDispatchOptions.SectionName))
            .Validate(IsValid, "OutboxDispatch configuration is invalid.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxDispatcherWorker>();
        return services;
    }

    private static bool IsValid(RabbitMqOptions options) =>
        !string.IsNullOrWhiteSpace(options.Host) &&
        options.Port > 0 &&
        !string.IsNullOrWhiteSpace(options.VirtualHost) &&
        !string.IsNullOrWhiteSpace(options.Username) &&
        !string.IsNullOrWhiteSpace(options.Password);

    private static bool IsValid(OutboxDispatchOptions options) =>
        options.BatchSize > 0 &&
        options.PollInterval > TimeSpan.Zero &&
        options.MaximumDeliveryAttempts > 0 &&
        options.InitialRetryDelay > TimeSpan.Zero &&
        options.MaximumRetryDelay >= options.InitialRetryDelay;
}
