using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Interfaces;

public interface IAuthenticationSessionManagementService
{
    Task<IReadOnlyList<
        AuthenticationSessionResponseDto>>
        GetActiveSessions();

    Task RevokeOwnedSession(Guid sessionId);

    Task RevokeOtherSessions();
}
