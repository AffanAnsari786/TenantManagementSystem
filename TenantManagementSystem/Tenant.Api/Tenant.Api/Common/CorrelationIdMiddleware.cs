using Serilog.Context;

namespace Tenant.Api.Common;

/// <summary>
/// Reads (or generates) a correlation ID per request and pushes it into
/// Serilog's LogContext so every log line emitted during the request is
/// tagged with {CorrelationId}. The same value is echoed back in the
/// <c>X-Correlation-ID</c> response header so clients can reference it when
/// filing bug reports.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
