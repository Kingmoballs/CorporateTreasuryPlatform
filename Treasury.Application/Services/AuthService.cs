using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

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

    private readonly IMultiFactorAuthenticationService
        _multiFactorAuthenticationService;

    private readonly IAuthenticationSessionService
        _sessionService;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IAuthenticationSecurityEventService
        _securityEventService;

    public AuthService(
        IUserRepository userRepository,
        ILoginAttemptService loginAttemptService,
        IMultiFactorAuthenticationService
            multiFactorAuthenticationService,
        IAuthenticationSessionService
            sessionService,
        ICurrentUserService currentUserService,
        IAuthenticationSecurityEventService
            securityEventService)
    {
        _userRepository = userRepository;

        _loginAttemptService =
            loginAttemptService;

        _multiFactorAuthenticationService =
            multiFactorAuthenticationService;

        _sessionService = sessionService;

        _currentUserService = currentUserService;

        _securityEventService =
            securityEventService;
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

            await RecordLoginEvent(
                dto.Email,
                null,
                null,
                AuthenticationSecurityEventTypes
                    .LoginFailed,
                AuthenticationSecurityOutcomes
                    .Failed,
                "invalid_credentials");

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

            var locked =
                user.LoginLockoutEndUtc.HasValue;

            await RecordLoginEvent(
                dto.Email,
                user.Id,
                GetCurrentMembership(user)?
                    .OrganizationId,
                locked
                    ? AuthenticationSecurityEventTypes
                        .LoginBlocked
                    : AuthenticationSecurityEventTypes
                        .LoginFailed,
                locked
                    ? AuthenticationSecurityOutcomes
                        .Blocked
                    : AuthenticationSecurityOutcomes
                        .Failed,
                locked
                    ? "failure_limit_reached"
                    : "invalid_credentials");

            throw InvalidCredentials();
        }

        if (!user.IsActive)
        {
            await RecordLoginEvent(
                dto.Email,
                user.Id,
                null,
                AuthenticationSecurityEventTypes
                    .LoginBlocked,
                AuthenticationSecurityOutcomes
                    .Blocked,
                "account_inactive");

            throw InvalidCredentials();
        }

        if (!user.EmailVerifiedAtUtc.HasValue)
        {
            await RecordLoginEvent(
                dto.Email,
                user.Id,
                null,
                AuthenticationSecurityEventTypes
                    .LoginBlocked,
                AuthenticationSecurityOutcomes
                    .Blocked,
                "email_unverified");

            throw InvalidCredentials();
        }

        var currentMembership =
            GetCurrentMembership(user);

        if (currentMembership is null)
        {
            await RecordLoginEvent(
                dto.Email,
                user.Id,
                null,
                AuthenticationSecurityEventTypes
                    .LoginBlocked,
                AuthenticationSecurityOutcomes
                    .Blocked,
                "membership_unavailable");

            throw InvalidCredentials();
        }

        var loginAllowed =
            await _loginAttemptService
                .CompleteSuccessfulAttempt(
                    user.Id);

        if (!loginAllowed)
        {
            await RecordLoginEvent(
                dto.Email,
                user.Id,
                currentMembership.OrganizationId,
                AuthenticationSecurityEventTypes
                    .LoginBlocked,
                AuthenticationSecurityOutcomes
                    .Blocked,
                "login_lockout_active");

            throw InvalidCredentials();
        }

        if (user.MfaEnabledAtUtc.HasValue)
        {
            return await
                _multiFactorAuthenticationService
                    .CreateLoginChallenge(
                        user,
                        currentMembership);
        }

        var tokens =
            await _sessionService.Create(
                user,
                currentMembership);

        await _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    currentMembership.OrganizationId,
                UserId = user.Id,
                AuthenticationSessionId =
                    tokens.AuthenticationSessionId,
                EventType =
                    AuthenticationSecurityEventTypes
                        .LoginSucceeded,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                Metadata = new
                {
                    authenticationMethod =
                        AuthenticationMethods.Password
                }
            });

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

        await RecordSessionRevocation(
            sessionId.Value,
            "user_logout",
            "current_session");
    }

    public async Task LogoutAll()
    {
        await _sessionService
            .RevokeSessionsForUser(
                _currentUserService.UserId,
                "User signed out from all sessions.");

        await RecordSessionRevocation(
            _currentUserService
                .AuthenticationSessionId,
            "user_logout_all",
            "all_sessions");
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
            OrganizationMembershipId =
                membership.Id,
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

    private Task RecordLoginEvent(
        string identifier,
        Guid? userId,
        Guid? organizationId,
        string eventType,
        string outcome,
        string reasonCode)
    {
        return _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId = organizationId,
                UserId = userId,
                EventType = eventType,
                Outcome = outcome,
                ReasonCode = reasonCode,
                Identifier = identifier
            });
    }

    private Task RecordSessionRevocation(
        Guid? sessionId,
        string reasonCode,
        string scope)
    {
        return _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    _currentUserService.OrganizationId,
                UserId =
                    _currentUserService.UserId,
                AuthenticationSessionId =
                    sessionId,
                EventType =
                    AuthenticationSecurityEventTypes
                        .SessionRevoked,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                ReasonCode = reasonCode,
                Metadata = new
                {
                    scope
                }
            });
    }
}
