using Treasury.Application.Common.Exceptions;

namespace Treasury.Infrastructure.Services;

internal static class PendingRequestExpiryGuard
{
    public static void EnsureNotExpired(
        DateTime? expiresAtUtc,
        string requestType)
    {
        if (expiresAtUtc.HasValue &&
            expiresAtUtc.Value <= DateTime.UtcNow)
        {
            throw new ConflictException(
                $"The {requestType} request has expired " +
                "and can no longer be approved or rejected.");
        }
    }
}