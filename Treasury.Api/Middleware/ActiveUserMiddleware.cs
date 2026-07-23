using System.Security.Claims;
using System.Text.Json;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

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
        IUserRepository userRepository,
        IAuthenticationSessionService
            sessionService)
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

            var organizationIdValue =
                context.User.FindFirstValue(
                    CustomClaimTypes.OrganizationId);

            var membershipIdValue =
                context.User.FindFirstValue(
                    CustomClaimTypes
                        .OrganizationMembershipId);

            var sessionIdValue =
                context.User.FindFirstValue(
                    CustomClaimTypes
                        .AuthenticationSessionId);

            if (!Guid.TryParse(
                userIdValue,
                out var userId) ||
                !Guid.TryParse(
                    organizationIdValue,
                    out var organizationId) ||
                !Guid.TryParse(
                    membershipIdValue,
                    out var membershipId) ||
                !Guid.TryParse(
                    sessionIdValue,
                    out var sessionId))
            {
                await RejectRequest(context);
                return;
            }

            var user =
                await userRepository
                    .GetById(userId);

            var membership =
                user?.OrganizationMemberships
                    .FirstOrDefault(item =>
                        item.Id == membershipId &&
                        item.OrganizationId ==
                            organizationId);

            var roleChanged =
                membership is not null &&
                !string.Equals(
                    membership.Role.Name,
                    tokenRole,
                    StringComparison
                        .OrdinalIgnoreCase);

            var sessionIsActive =
                user is not null &&
                membership is not null &&
                await sessionService
                    .IsSessionActive(
                        sessionId,
                        userId,
                        membershipId);

            if (user is null ||
                !user.IsActive ||
                !user.EmailVerifiedAtUtc.HasValue ||
                membership is null ||
                !membership.IsActive ||
                !membership.Organization.IsActive ||
                roleChanged ||
                !sessionIsActive)
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
