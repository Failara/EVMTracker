using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EVMTracker.Observability;

internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        string? supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        Guid correlationId = Guid.TryParse(supplied, out Guid parsed) && parsed != Guid.Empty
            ? parsed
            : Guid.CreateVersion7();
        context.Response.Headers[HeaderName] = correlationId.ToString("D");
        Activity.Current?.SetTag("correlation.id", correlationId.ToString("D"));

        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
            });
        await next(context).ConfigureAwait(false);
    }
}
