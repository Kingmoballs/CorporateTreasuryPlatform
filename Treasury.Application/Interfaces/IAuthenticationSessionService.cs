using Treasury.Application.DTOs.Auth;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IAuthenticationSessionService
{
    Task<AuthenticationTokenPairDto> Create(
        User user,
        OrganizationMembership membership);

    Task<AuthenticationTokenPairDto> Create(
        User user,
        OrganizationMembership membership,
        string authenticationMethod);

    Task<AuthenticationTokenPairDto>
        SwitchOrganization(
            User user,
            OrganizationMembership membership,
            Guid currentSessionId);

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
