using System.Security.Claims;
using System.Text.Json;
using Treasury.Application.Interfaces;

namespace Treasury.Api.Middleware;

public class ActiveUserMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveUserMiddleware(
        RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IUserRepository userRepository)
    {
        if (context.User.Identity?
                .IsAuthenticated == true)
        {
            var userIdValue =
                context.User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var tokenRole =
                context.User.FindFirstValue(
                    ClaimTypes.Role);

            if (!Guid.TryParse(
                userIdValue,
                out var userId))
            {
                await RejectRequest(context);
                return;
            }

            var user =
                await userRepository
                    .GetById(userId);

            var roleChanged =
                user is not null &&
                !string.Equals(
                    user.Role.Name,
                    tokenRole,
                    StringComparison
                        .OrdinalIgnoreCase);

            if (user is null ||
                !user.IsActive ||
                roleChanged)
            {
                await RejectRequest(context);
                return;
            }
        }

        await _next(context);
    }

    private static async Task RejectRequest(
        HttpContext context)
    {
        context.Response.StatusCode =
            StatusCodes.Status401Unauthorized;

        context.Response.ContentType =
            "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new
            {
                success = false,

                message =
                    "Your account or role changed. " +
                    "Please sign in again."
            }));
    }
}