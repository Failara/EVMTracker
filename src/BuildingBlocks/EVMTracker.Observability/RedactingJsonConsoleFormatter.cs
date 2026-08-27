using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace EVMTracker.Observability;

public sealed class RedactingJsonConsoleFormatterOptions : ConsoleFormatterOptions;

public sealed class RedactingJsonConsoleFormatter(
    IOptionsMonitor<RedactingJsonConsoleFormatterOptions> options)
    : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "evmtracker-json";

    private readonly RedactingJsonConsoleFormatterOptions options = options.CurrentValue;

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);

        Activity? activity = Activity.Current;
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["timestamp"] = DateTimeOffset.UtcNow,
            ["level"] = logEntry.LogLevel.ToString(),
            ["category"] = logEntry.Category,
            ["eventId"] = logEntry.EventId.Id,
            ["message"] = SensitiveDataRedactor.Redact(
                null,
                logEntry.Formatter(logEntry.State, logEntry.Exception)),
        };

        if (activity is not null)
        {
            payload["traceId"] = activity.TraceId.ToHexString();
            payload["spanId"] = activity.SpanId.ToHexString();
        }

        AddProperties(payload, "properties", logEntry.State);
        if (options.IncludeScopes && scopeProvider is not null)
        {
            var scopes = new List<IReadOnlyDictionary<string, string>>();
            scopeProvider.ForEachScope(
                static (scope, collection) => collection.Add(ReadProperties(scope)),
                scopes);
            if (scopes.Count > 0)
            {
                payload["scopes"] = scopes;
            }
        }

        if (logEntry.Exception is not null)
        {
            payload["exceptionType"] = logEntry.Exception.GetType().FullName;
            payload["exceptionMessage"] = SensitiveDataRedactor.Redact(
                null,
                logEntry.Exception.Message);
        }

        textWriter.WriteLine(JsonSerializer.Serialize(payload));
    }

    private static void AddProperties<TState>(
        Dictionary<string, object?> payload,
        string propertyName,
        TState state)
    {
        Dictionary<string, string> properties = ReadProperties(state);
        if (properties.Count > 0)
        {
            payload[propertyName] = properties;
        }
    }

    private static Dictionary<string, string> ReadProperties(object? state)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (state is not IEnumerable<KeyValuePair<string, object?>> values)
        {
            return properties;
        }

        foreach (KeyValuePair<string, object?> item in values)
        {
            if (item.Key == "{OriginalFormat}")
            {
                continue;
            }

            string text = Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            properties[item.Key] = SensitiveDataRedactor.Redact(item.Key, text);
        }

        return properties;
    }
}
