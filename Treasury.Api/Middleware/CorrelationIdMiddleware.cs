using System.Diagnostics;

namespace Treasury.Api.Middleware;

public class CorrelationIdMiddleware
{
    public const string HeaderName =
        "X-Correlation-ID";

    private const int MaximumLength = 128;

    private readonly RequestDelegate _next;

    private readonly ILogger<CorrelationIdMiddleware>
        _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            GetCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] =
            correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] =
                correlationId;
            return Task.CompletedTask;
        });

        using var scope = _logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            });

        var startedAt =
            Stopwatch.GetTimestamp();

        try
        {
            await _next(context);
        }
        finally
        {
            _logger.LogInformation(
                "HTTP {Method} {Path} completed with " +
                "{StatusCode} in {ElapsedMilliseconds} " +
                "ms. Correlation ID: {CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt)
                    .TotalMilliseconds,
                correlationId);
        }
    }

    private static string GetCorrelationId(
        HttpContext context)
    {
        var supplied =
            context.Request.Headers[HeaderName]
                .FirstOrDefault();

        return IsValid(supplied)
            ? supplied!
            : Guid.NewGuid().ToString("N");
    }

    private static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaximumLength)
        {
            return false;
        }

        return value.All(character =>
            char.IsLetterOrDigit(character) ||
            character is '-' or '_' or '.' or ':' or '/');
    }
}
