using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EVMTracker.Observability;

public static class ServiceDefaultsExtensions
{
    private const string LivenessTag = "live";

    public static IServiceCollection AddServiceDefaults(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: [LivenessTag]);

        return services;
    }

    public static WebApplication MapServiceDefaults(this WebApplication app)
    {
        app.MapHealthChecks(
            "/health/live",
            new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(LivenessTag),
            });

        app.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions
            {
                Predicate = _ => true,
            });

        return app;
    }
}
