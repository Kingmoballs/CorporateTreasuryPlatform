using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

namespace Treasury.Application.Services;

public class AuthService : IAuthService
{
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword(
            "non-account-password-verification-value");

    private readonly IUserRepository
        _userRepository;

    private readonly ILoginAttemptService
        _loginAttemptService;

    private readonly IAuthenticationSessionService
        _sessionService;

    private readonly ICurrentUserService
        _currentUserService;

    public AuthService(
        IUserRepository userRepository,
        ILoginAttemptService loginAttemptService,
        IAuthenticationSessionService
            sessionService,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;

        _loginAttemptService =
            loginAttemptService;

        _sessionService = sessionService;

        _currentUserService = currentUserService;
    }

    public async Task<AuthResponseDto>
        Login(LoginDto dto)
    {
        var user =
            await _userRepository
                .GetByEmail(dto.Email);

        if(user == null)
        {
            /*
             * Perform a real BCrypt verification even when
             * the account is unknown to reduce observable
             * timing differences used for enumeration.
             */
            _ = BCrypt.Net.BCrypt.Verify(
                dto.Password,
                DummyPasswordHash);

            throw InvalidCredentials();
        }

        var validPassword =
            BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash);

        if(!validPassword)
        {
            await _loginAttemptService
                .RecordFailure(user.Id);

            throw InvalidCredentials();
        }

        if (!user.IsActive)
        {
            throw InvalidCredentials();
        }

        if (!user.EmailVerifiedAtUtc.HasValue)
        {
            throw InvalidCredentials();
        }

        var currentMembership =
            GetCurrentMembership(user);

        if (currentMembership is null)
        {
            throw InvalidCredentials();
        }

        var loginAllowed =
            await _loginAttemptService
                .CompleteSuccessfulAttempt(
                    user.Id);

        if (!loginAllowed)
        {
            throw InvalidCredentials();
        }

        var tokens =
            await _sessionService.Create(
                user,
                currentMembership);

        return MapResponse(
            user,
            currentMembership,
            tokens);
    }

    public Task<AuthResponseDto> Refresh(
        RefreshTokenDto dto)
    {
        return _sessionService.Refresh(
            dto.RefreshToken);
    }

    public async Task Logout()
    {
        var sessionId =
            _currentUserService
                .AuthenticationSessionId;

        if (!sessionId.HasValue ||
            sessionId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "A valid authentication session is " +
                "required.");
        }

        await _sessionService.RevokeSession(
            sessionId.Value,
            "User signed out.");
    }

    public async Task LogoutAll()
    {
        await _sessionService
            .RevokeSessionsForUser(
                _currentUserService.UserId,
                "User signed out from all sessions.");
    }

    private static AuthResponseDto MapResponse(
        User user,
        OrganizationMembership membership,
        AuthenticationTokenPairDto tokens)
    {
        return new AuthResponseDto
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            AccessTokenExpiresAtUtc =
                tokens.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc =
                tokens.RefreshTokenExpiresAtUtc,
            Email = user.Email,
            Role = membership.Role.Name,
            OrganizationId =
                membership.OrganizationId,
            OrganizationCode =
                membership.Organization.Code
        };
    }

    /*
     * The default active membership determines the tenant
     * and role represented by the issued access token.
     */
    private static OrganizationMembership?
        GetCurrentMembership(User user)
    {
        return user.OrganizationMemberships
            .Where(membership =>
                membership.IsActive &&
                membership.Organization.IsActive)
            .OrderByDescending(membership =>
                membership.IsDefault)
            .ThenBy(membership =>
                membership.JoinedAtUtc)
            .FirstOrDefault();
    }

    private static UnauthorizedAccessException
        InvalidCredentials()
    {
        return new UnauthorizedAccessException(
            "Invalid credentials.");
    }
}
