using System.Net;
using System.Text.Json;

namespace Treasury.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(
        RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleException(
                context,
                ex);
        }
    }

    private static async Task HandleException(
        HttpContext context,
        Exception exception)
    {
        context.Response.ContentType =
            "application/json";

        context.Response.StatusCode =
            (int)HttpStatusCode.BadRequest;

        var response = new
        {
            success = false,
            message = exception.Message
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}