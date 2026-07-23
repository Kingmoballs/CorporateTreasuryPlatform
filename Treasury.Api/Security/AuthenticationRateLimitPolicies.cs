using System.Threading.RateLimiting;

namespace Treasury.Api.Security;

public static class AuthenticationRateLimitPolicies
{
    public const string Login = "authentication-login";

    public const string Refresh =
        "authentication-refresh";

    public const string PasswordRecovery =
        "authentication-password-recovery";

    public static RateLimitPartition<string>
        CreateFixedWindowPartition(
            HttpContext context,
            int requestsPerMinute)
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
                            requestsPerMinute,
                        QueueLimit = 0,
                        Window =
                            TimeSpan.FromMinutes(1)
                    });
    }
}
