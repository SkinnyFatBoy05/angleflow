using System.Diagnostics;

namespace AngleFlow.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsSafe(supplied) ? supplied! : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag("angleflow.correlation_id", correlationId);

        await next(context);
    }

    private static bool IsSafe(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 80
        && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
}
