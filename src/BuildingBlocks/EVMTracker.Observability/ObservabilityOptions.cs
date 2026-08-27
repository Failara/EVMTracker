namespace EVMTracker.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool EnablePrometheus { get; init; } = true;

    public string PrometheusPath { get; init; } = "/metrics";

    public string? OtlpEndpoint { get; init; }

    public double TraceSamplingRatio { get; init; } = 1;
}
