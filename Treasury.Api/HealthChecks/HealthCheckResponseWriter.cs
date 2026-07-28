using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Treasury.Api.HealthChecks;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions
        JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task Write(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType =
            "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMilliseconds =
                Math.Round(
                    report.TotalDuration
                        .TotalMilliseconds,
                    2),
            checks = report.Entries
                .OrderBy(entry => entry.Key)
                .Select(entry => new
                {
                    name = entry.Key,
                    status =
                        entry.Value.Status.ToString(),
                    durationMilliseconds =
                        Math.Round(
                            entry.Value.Duration
                                .TotalMilliseconds,
                            2)
                })
                .ToArray()
        };

        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            payload,
            JsonOptions);
    }
}
