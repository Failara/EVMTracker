using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EVMTracker.Observability;

public static class ServiceDefaultsExtensions
{
    private const string LivenessTag = "live";

    public static IServiceCollection AddServiceDefaults(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        ObservabilityOptions configured = configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        double samplingRatio = configured.TraceSamplingRatio is >= 0 and <= 1
            ? configured.TraceSamplingRatio
            : 1;
        string prometheusPath = !string.IsNullOrWhiteSpace(configured.PrometheusPath) &&
            configured.PrometheusPath[0] == '/'
                ? configured.PrometheusPath
                : "/metrics";
        services.AddOptions<ObservabilityOptions>()
            .Bind(configuration.GetSection(ObservabilityOptions.SectionName))
            .Validate(IsValid, "Observability configuration is invalid.")
            .ValidateOnStart();

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsoleFormatter<
                RedactingJsonConsoleFormatter,
                RedactingJsonConsoleFormatterOptions>(options => options.IncludeScopes = true);
            logging.AddConsole(options =>
                options.FormatterName = RedactingJsonConsoleFormatter.FormatterName);
        });

        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName));
        telemetry.WithTracing(tracing =>
        {
            tracing
                .SetSampler(new TraceIdRatioBasedSampler(samplingRatio))
                .AddProcessor(new SensitiveActivityProcessor())
                .AddSource(EVMTrackerTelemetry.ActivitySourceName, "MassTransit")
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !IsTelemetryEndpoint(
                        context.Request.Path,
                        prometheusPath))
                .AddHttpClientInstrumentation();
            if (TryGetOtlpEndpoint(configured, out Uri? endpoint))
            {
                tracing.AddOtlpExporter(exporter => exporter.Endpoint = endpoint!);
            }
        });
        telemetry.WithMetrics(metrics =>
        {
            metrics
                .AddMeter(EVMTrackerTelemetry.MeterName, "MassTransit")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();
            if (configured.EnablePrometheus)
            {
                metrics.AddPrometheusExporter(options =>
                    options.ScrapeEndpointPath = prometheusPath);
            }

            if (TryGetOtlpEndpoint(configured, out Uri? endpoint))
            {
                metrics.AddOtlpExporter(exporter => exporter.Endpoint = endpoint!);
            }
        });

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
        app.UseMiddleware<CorrelationIdMiddleware>();
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

        ObservabilityOptions options = app.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<ObservabilityOptions>>()
            .Value;
        if (options.EnablePrometheus)
        {
            app.MapPrometheusScrapingEndpoint();
        }

        return app;
    }

    private static bool IsValid(ObservabilityOptions options) =>
        options.TraceSamplingRatio is >= 0 and <= 1 &&
        !string.IsNullOrWhiteSpace(options.PrometheusPath) &&
        options.PrometheusPath[0] == '/' &&
        (string.IsNullOrWhiteSpace(options.OtlpEndpoint) ||
            TryGetOtlpEndpoint(options, out _));

    private static bool TryGetOtlpEndpoint(
        ObservabilityOptions options,
        out Uri? endpoint) =>
        Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out endpoint) &&
        (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps);

    private static bool IsTelemetryEndpoint(string path, string prometheusPath) =>
        path.Equals("/health/live", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase) ||
        path.Equals(prometheusPath, StringComparison.OrdinalIgnoreCase);
}
