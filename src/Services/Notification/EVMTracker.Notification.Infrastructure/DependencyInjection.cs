using EVMTracker.Messaging;
using EVMTracker.Notification.Application.Watchlists;
using EVMTracker.Notification.Infrastructure.Configuration;
using EVMTracker.Notification.Infrastructure.Messaging;
using EVMTracker.Notification.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EVMTracker.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddOptions<NotificationMessagingOptions>()
            .Bind(configuration.GetSection(NotificationMessagingOptions.SectionName))
            .Validate(IsValid, "NotificationMessaging configuration is invalid.")
            .ValidateOnStart();
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    "notification")));
        services.AddScoped<IWatchlistProjectionStore, WatchlistProjectionStore>();
        services.AddRabbitMqMessaging(
            configuration,
            registration => registration.AddConsumer<WatchlistProjectionConsumer>(),
            (broker, context, _) =>
            {
                NotificationMessagingOptions options = context.GetRequiredService<
                    IOptions<NotificationMessagingOptions>>().Value;
                broker.ReceiveEndpoint(
                    options.WatchlistProjectionQueue,
                    endpoint =>
                    {
                        endpoint.Durable = true;
                        endpoint.AutoDelete = false;
                        endpoint.PrefetchCount = options.PrefetchCount;
                        endpoint.ConcurrentMessageLimit = 1;
                        endpoint.UseMessageRetry(retry =>
                            retry.Intervals(options.RetryIntervals));
                        endpoint.ConfigureConsumer<WatchlistProjectionConsumer>(context);
                    });
            });

        return services;
    }

    private static bool IsValid(NotificationMessagingOptions options) =>
        !string.IsNullOrWhiteSpace(options.WatchlistProjectionQueue) &&
        options.PrefetchCount > 0 &&
        options.RetryIntervals.Length > 0 &&
        options.RetryIntervals.All(interval => interval > TimeSpan.Zero);
}
