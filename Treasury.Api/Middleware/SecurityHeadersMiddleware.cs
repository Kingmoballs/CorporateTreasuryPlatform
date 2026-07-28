namespace Treasury.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ApplyHeaders(context.Response.Headers);

        context.Response.OnStarting(() =>
        {
            ApplyHeaders(context.Response.Headers);
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private void ApplyHeaders(
        IHeaderDictionary headers)
    {
        headers.TryAdd(
            "X-Content-Type-Options",
            "nosniff");
        headers.TryAdd(
            "X-Frame-Options",
            "DENY");
        headers.TryAdd(
            "Referrer-Policy",
            "no-referrer");
        headers.TryAdd(
            "Permissions-Policy",
            "camera=(), microphone=(), " +
            "geolocation=()");
        headers.TryAdd(
            "X-Permitted-Cross-Domain-Policies",
            "none");

        if (_environment.IsProduction())
        {
            headers.TryAdd(
                "Content-Security-Policy",
                "default-src 'none'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'none'; " +
                "form-action 'none'");
        }
    }
}
