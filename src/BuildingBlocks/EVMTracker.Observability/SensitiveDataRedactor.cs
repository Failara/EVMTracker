using System.Text.RegularExpressions;

namespace EVMTracker.Observability;

public static partial class SensitiveDataRedactor
{
    public const string RedactedValue = "[REDACTED]";

    public static string Redact(string? key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        if (IsSensitiveKey(key))
        {
            return RedactedValue;
        }

        string redacted = BotTokenPattern().Replace(value, RedactedValue);
        redacted = BearerTokenPattern().Replace(redacted, "$1" + RedactedValue);
        redacted = ConnectionSecretPattern().Replace(redacted, "$1=" + RedactedValue);
        redacted = QuerySecretPattern().Replace(redacted, "$1=" + RedactedValue);
        return UriUserInfoPattern().Replace(redacted, "$1" + RedactedValue + "@");
    }

    public static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("connectionstring", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("rpcurl", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("providerurl", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("telegramuserid", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("telegramchatid", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("telegramupdateid", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("userid", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("chatid", StringComparison.OrdinalIgnoreCase);
    }

    public static string RedactTelemetryAttribute(string key, string? value)
    {
        if (key.Equals("url.full", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("http.url", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("url.query", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("db.connection_string", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("enduser.id", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("authorization", StringComparison.OrdinalIgnoreCase))
        {
            return RedactedValue;
        }

        return Redact(key, value);
    }

    [GeneratedRegex(@"\b\d{5,}:[A-Za-z0-9_-]{20,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex BotTokenPattern();

    [GeneratedRegex(@"(?i)\b(Bearer\s+)[^\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"(?i)\b(Password|Pwd|User\s*Id|Username)\s*=\s*[^;\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionSecretPattern();

    [GeneratedRegex(@"(?i)\b(api[-_]?key|access[-_]?token|token|secret|password|authorization)\s*=\s*[^&\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex QuerySecretPattern();

    [GeneratedRegex(@"(?i)\b(https?://)[^/@\s]+@", RegexOptions.CultureInvariant)]
    private static partial Regex UriUserInfoPattern();
}
