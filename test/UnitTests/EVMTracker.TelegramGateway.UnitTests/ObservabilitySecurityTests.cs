using System.Diagnostics;
using EVMTracker.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EVMTracker.TelegramGateway.UnitTests;

public sealed class ObservabilitySecurityTests
{
    [Fact]
    public void RedactorRemovesSecretsAndTelegramIdentifiers()
    {
        const string botToken = "123456789:abcdefghijklmnopqrstuvwxyzABCDE";
        const string connection = "Host=db;Username=admin;Password=super-secret";
        const string providerUrl = "https://rpc-user:rpc-password@rpc.example/eth?apiKey=abc123";

        string redacted = SensitiveDataRedactor.Redact(
            null,
            $"{botToken} {connection} {providerUrl}");

        Assert.DoesNotContain(botToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("rpc-password", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.Equal(
            SensitiveDataRedactor.RedactedValue,
            SensitiveDataRedactor.Redact(
                "RpcUrl",
                "https://provider.example/v2/path-based-api-key"));
        Assert.Equal(
            SensitiveDataRedactor.RedactedValue,
            SensitiveDataRedactor.Redact("TelegramChatId", "987654321"));
    }

    [Fact]
    public void ConsoleFormatterDoesNotWriteSensitiveValues()
    {
        const string botToken = "123456789:abcdefghijklmnopqrstuvwxyzABCDE";
        var formatter = new RedactingJsonConsoleFormatter(
            new StaticOptionsMonitor<RedactingJsonConsoleFormatterOptions>(
                new RedactingJsonConsoleFormatterOptions()));
        var entry = new LogEntry<IReadOnlyList<KeyValuePair<string, object?>>>(
            LogLevel.Warning,
            "security-test",
            new EventId(1),
            new Dictionary<string, object?>
            {
                ["BotToken"] = botToken,
                ["TelegramUserId"] = 123456789L,
                ["{OriginalFormat}"] = "Secret {BotToken}",
            }.ToArray(),
            null,
            static (state, _) => $"Secret {state[0].Value}");
        using var writer = new StringWriter();

        formatter.Write(entry, null, writer);

        string output = writer.ToString();
        Assert.DoesNotContain(botToken, output, StringComparison.Ordinal);
        Assert.DoesNotContain("123456789", output, StringComparison.Ordinal);
        Assert.Contains(SensitiveDataRedactor.RedactedValue, output, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceProcessorRedactsCredentialBearingUrls()
    {
        using var activity = new Activity("sensitive-http-request");
        activity.SetTag("url.full", "https://api.telegram.org/bot123456:secret/getUpdates");
        activity.SetTag("http.route", "/safe-route");
        var processor = new SensitiveActivityProcessor();

        processor.OnEnd(activity);

        Assert.Equal(
            SensitiveDataRedactor.RedactedValue,
            activity.GetTagItem("url.full"));
        Assert.Equal("/safe-route", activity.GetTagItem("http.route"));
    }

    [Fact]
    public void InvalidObservabilityConfigurationFailsValidation()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Observability:TraceSamplingRatio"] = "2",
                    ["Observability:PrometheusPath"] = "metrics",
                    ["Observability:OtlpEndpoint"] = "file:///tmp/traces",
                })
            .Build();
        var services = new ServiceCollection();
        services.AddServiceDefaults(configuration, "EVMTracker.SecurityTests");
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ObservabilityOptions>>().Value);
    }

    [Fact]
    public void IntegrationActivityCarriesStableEventAndCorrelationIdentity()
    {
        Guid eventId = Guid.NewGuid();
        Guid correlationId = Guid.NewGuid();
        Guid causationId = Guid.NewGuid();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == EVMTrackerTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using Activity? activity = EVMTrackerTelemetry.StartIntegrationEventActivity(
            "consume",
            "transfers.erc20-observed.v1",
            eventId,
            correlationId,
            causationId,
            ActivityKind.Consumer);

        Assert.NotNull(activity);
        Assert.Equal(eventId.ToString("D"), activity.GetTagItem("messaging.message.id"));
        Assert.Equal(correlationId.ToString("D"), activity.GetTagItem("correlation.id"));
        Assert.Equal(causationId.ToString("D"), activity.GetTagItem("causation.id"));
    }

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = currentValue;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
