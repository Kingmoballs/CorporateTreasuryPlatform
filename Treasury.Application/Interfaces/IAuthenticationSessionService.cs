using Treasury.Application.DTOs.Auth;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IAuthenticationSessionService
{
    Task<AuthenticationTokenPairDto> Create(
        User user,
        OrganizationMembership membership);

    Task<AuthResponseDto> Refresh(
        string rawRefreshToken);

    Task<bool> IsSessionActive(
        Guid sessionId,
        Guid userId,
        Guid organizationMembershipId);

    Task RevokeSession(
        Guid sessionId,
        string reason);

    Task RevokeSessionsForMembership(
        Guid organizationMembershipId,
        string reason);

    Task RevokeSessionsForUser(
        Guid userId,
        string reason);
}
