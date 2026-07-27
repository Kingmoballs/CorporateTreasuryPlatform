using System.Threading.RateLimiting;

namespace Treasury.Api.Security;

public static class AuthenticationRateLimitPolicies
{
    public const string Login = "authentication-login";

    public const string Refresh =
        "authentication-refresh";

    public const string PasswordRecovery =
        "authentication-password-recovery";

    public const string MultiFactorAuthentication =
        "authentication-mfa";

    public const string OrganizationApplication =
        "organization-application";

    public static RateLimitPartition<string>
        CreateFixedWindowPartition(
            HttpContext context,
            int requestsPerMinute)
    {
        return CreateFixedWindowPartition(
            context,
            requestsPerMinute,
            TimeSpan.FromMinutes(1));
    }

    public static RateLimitPartition<string>
        CreateFixedWindowPartition(
            HttpContext context,
            int permitLimit,
            TimeSpan window)
    {
        var clientAddress =
            context.Connection.RemoteIpAddress?
                .ToString() ??
            "unknown";

        return RateLimitPartition
            .GetFixedWindowLimiter(
                clientAddress,
                _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit =
                            permitLimit,
                        QueueLimit = 0,
                        Window =
                            window
                    });
    }
}
