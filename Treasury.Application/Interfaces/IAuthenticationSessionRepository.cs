using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IAuthenticationSessionRepository
{
    Task Add(
        AuthenticationSession session,
        AuthenticationRefreshToken refreshToken);

    Task<AuthenticationRefreshToken?>
        GetRefreshTokenByHash(string tokenHash);

    Task<bool> RotateRefreshToken(
        Guid currentTokenId,
        AuthenticationRefreshToken replacement,
        DateTime consumedAtUtc);

    Task<bool> ReplaceSession(
        Guid currentSessionId,
        Guid userId,
        AuthenticationSession replacementSession,
        AuthenticationRefreshToken replacementToken,
        DateTime replacedAtUtc,
        string reason);

    Task<bool> IsSessionActive(
        Guid sessionId,
        Guid userId,
        Guid organizationMembershipId,
        DateTime nowUtc);

    Task RevokeSession(
        Guid sessionId,
        DateTime revokedAtUtc,
        string reason);

    Task RevokeSessionsForMembership(
        Guid organizationMembershipId,
        DateTime revokedAtUtc,
        string reason);

    Task RevokeSessionsForUser(
        Guid userId,
        DateTime revokedAtUtc,
        string reason);

    Task<IReadOnlyList<AuthenticationSession>>
        GetActiveSessionsForUser(
            Guid userId,
            DateTime nowUtc);

    Task<bool> RevokeOwnedSession(
        Guid sessionId,
        Guid userId,
        DateTime revokedAtUtc,
        string reason);

    Task<int> RevokeOtherSessions(
        Guid userId,
        Guid currentSessionId,
        DateTime revokedAtUtc,
        string reason);

    Task SaveChanges();
}
