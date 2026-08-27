using System.Diagnostics;
using System.Globalization;
using OpenTelemetry;

namespace EVMTracker.Observability;

public sealed class SensitiveActivityProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (KeyValuePair<string, object?> tag in data.TagObjects.ToArray())
        {
            string text = Convert.ToString(tag.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            string redacted = SensitiveDataRedactor.RedactTelemetryAttribute(tag.Key, text);
            if (!string.Equals(text, redacted, StringComparison.Ordinal))
            {
                data.SetTag(tag.Key, redacted);
            }
        }
    }
}
