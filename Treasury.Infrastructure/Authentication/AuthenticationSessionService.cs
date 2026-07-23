using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Authentication;

public class AuthenticationSessionService
    : IAuthenticationSessionService
{
    private const int TokenByteCount = 32;

    private readonly IAuthenticationSessionRepository
        _repository;

    private readonly IJwtService _jwtService;

    private readonly JwtSettingsOptions _jwtOptions;

    private readonly AuthenticationSessionOptions
        _sessionOptions;

    private readonly TimeProvider _timeProvider;

    private readonly IClientRequestContext
        _clientRequestContext;

    private readonly IAuthenticationSecurityEventService
        _securityEventService;

    public AuthenticationSessionService(
        IAuthenticationSessionRepository
            repository,
        IJwtService jwtService,
        IOptions<JwtSettingsOptions> jwtOptions,
        IOptions<AuthenticationSessionOptions>
            sessionOptions,
        TimeProvider timeProvider,
        IClientRequestContext clientRequestContext,
        IAuthenticationSecurityEventService
            securityEventService)
    {
        _repository = repository;
        _jwtService = jwtService;
        _jwtOptions = jwtOptions.Value;
        _sessionOptions = sessionOptions.Value;
        _timeProvider = timeProvider;
        _clientRequestContext =
            clientRequestContext;
        _securityEventService =
            securityEventService;
    }

    public Task<AuthenticationTokenPairDto> Create(
            User user,
            OrganizationMembership membership)
    {
        return Create(
            user,
            membership,
            AuthenticationMethods.Password);
    }

    public async Task<AuthenticationTokenPairDto>
        Create(
            User user,
            OrganizationMembership membership,
            string authenticationMethod)
    {
        var now = GetUtcNow();
        var sessionExpiry =
            now.AddDays(
                _sessionOptions.RefreshTokenDays);

        var session =
            new AuthenticationSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OrganizationId =
                    membership.OrganizationId,
                OrganizationMembershipId =
                    membership.Id,
                CreatedAtUtc = now,
                LastActivityAtUtc = now,
                ExpiresAtUtc = sessionExpiry,
                SecurityStamp = user.SecurityStamp,
                AuthenticationMethod =
                    authenticationMethod,
                IpAddress =
                    _clientRequestContext.IpAddress,
                UserAgent =
                    _clientRequestContext.UserAgent
            };

        var rawRefreshToken = GenerateToken();

        var refreshToken =
            new AuthenticationRefreshToken
            {
                Id = Guid.NewGuid(),
                AuthenticationSessionId =
                    session.Id,
                AuthenticationSession = session,
                TokenHash =
                    HashToken(rawRefreshToken),
                CreatedAtUtc = now,
                ExpiresAtUtc = sessionExpiry
            };

        session.RefreshTokens.Add(refreshToken);

        await _repository.Add(
            session,
            refreshToken);

        await _repository.SaveChanges();

        var tokenPair = CreateTokenPair(
            user,
            membership,
            session.Id,
            rawRefreshToken,
            sessionExpiry,
            now);

        await _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    membership.OrganizationId,
                UserId = user.Id,
                AuthenticationSessionId =
                    session.Id,
                EventType =
                    AuthenticationSecurityEventTypes
                        .SessionCreated,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                Metadata = new
                {
                    authenticationMethod
                }
            });

        return tokenPair;
    }

    public async Task<AuthenticationTokenPairDto>
        SwitchOrganization(
            User user,
            OrganizationMembership membership,
            Guid currentSessionId)
    {
        var now = GetUtcNow();
        var sessionExpiry =
            now.AddDays(
                _sessionOptions.RefreshTokenDays);

        var replacementSession =
            new AuthenticationSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OrganizationId =
                    membership.OrganizationId,
                OrganizationMembershipId =
                    membership.Id,
                CreatedAtUtc = now,
                LastActivityAtUtc = now,
                ExpiresAtUtc = sessionExpiry,
                SecurityStamp = user.SecurityStamp,
                AuthenticationMethod =
                    AuthenticationMethods
                        .OrganizationSwitch,
                IpAddress =
                    _clientRequestContext.IpAddress,
                UserAgent =
                    _clientRequestContext.UserAgent
            };

        var rawRefreshToken = GenerateToken();

        var replacementToken =
            new AuthenticationRefreshToken
            {
                Id = Guid.NewGuid(),
                AuthenticationSessionId =
                    replacementSession.Id,
                AuthenticationSession =
                    replacementSession,
                TokenHash =
                    HashToken(rawRefreshToken),
                CreatedAtUtc = now,
                ExpiresAtUtc = sessionExpiry
            };

        replacementSession.RefreshTokens.Add(
            replacementToken);

        var replaced =
            await _repository.ReplaceSession(
                currentSessionId,
                user.Id,
                replacementSession,
                replacementToken,
                now,
                "Session replaced during " +
                "organization switch.");

        if (!replaced)
        {
            throw new UnauthorizedAccessException(
                "The current session can no longer be " +
                "used to switch organizations.");
        }

        var tokenPair = CreateTokenPair(
            user,
            membership,
            replacementSession.Id,
            rawRefreshToken,
            sessionExpiry,
            now);

        await _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    membership.OrganizationId,
                UserId = user.Id,
                AuthenticationSessionId =
                    replacementSession.Id,
                EventType =
                    AuthenticationSecurityEventTypes
                        .SessionCreated,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded,
                Metadata = new
                {
                    authenticationMethod =
                        AuthenticationMethods
                            .OrganizationSwitch
                }
            });

        return tokenPair;
    }

    public async Task<AuthResponseDto> Refresh(
        string rawRefreshToken)
    {
        var token =
            await _repository
                .GetRefreshTokenByHash(
                    HashToken(rawRefreshToken));

        if (token is null)
        {
            throw InvalidRefreshToken();
        }

        var now = GetUtcNow();
        var session =
            token.AuthenticationSession;

        if (token.ConsumedAtUtc.HasValue ||
            token.RevokedAtUtc.HasValue)
        {
            await _repository.RevokeSession(
                session.Id,
                now,
                "Refresh token reuse detected.");

            await RecordRefreshTokenReuse(
                session,
                "refresh_token_reused");

            throw InvalidRefreshToken();
        }

        var user = session.User;
        var membership =
            session.OrganizationMembership;

        if (token.ExpiresAtUtc <= now ||
            session.ExpiresAtUtc <= now ||
            session.RevokedAtUtc.HasValue ||
            session.SecurityStamp !=
                user.SecurityStamp ||
            !user.IsActive ||
            !user.EmailVerifiedAtUtc.HasValue ||
            !membership.IsActive ||
            !membership.Organization.IsActive)
        {
            await _repository.RevokeSession(
                session.Id,
                now,
                "Session is no longer eligible.");

            throw InvalidRefreshToken();
        }

        var replacementRawToken =
            GenerateToken();

        var replacement =
            new AuthenticationRefreshToken
            {
                Id = Guid.NewGuid(),
                AuthenticationSessionId =
                    session.Id,
                TokenHash =
                    HashToken(replacementRawToken),
                CreatedAtUtc = now,
                ExpiresAtUtc =
                    session.ExpiresAtUtc
            };

        var rotated =
            await _repository.RotateRefreshToken(
                token.Id,
                replacement,
                now);

        if (!rotated)
        {
            await _repository.RevokeSession(
                session.Id,
                now,
                "Concurrent refresh-token reuse " +
                "detected.");

            await RecordRefreshTokenReuse(
                session,
                "concurrent_refresh_token_reuse");

            throw InvalidRefreshToken();
        }

        var tokens = CreateTokenPair(
            user,
            membership,
            session.Id,
            replacementRawToken,
            session.ExpiresAtUtc,
            now);

        await _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    session.OrganizationId,
                UserId = session.UserId,
                AuthenticationSessionId =
                    session.Id,
                EventType =
                    AuthenticationSecurityEventTypes
                        .SessionRefreshed,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Succeeded
            });

        return MapResponse(
            user,
            membership,
            tokens);
    }

    public Task<bool> IsSessionActive(
        Guid sessionId,
        Guid userId,
        Guid organizationMembershipId)
    {
        return _repository.IsSessionActive(
            sessionId,
            userId,
            organizationMembershipId,
            GetUtcNow());
    }

    public Task RevokeSession(
        Guid sessionId,
        string reason)
    {
        return _repository.RevokeSession(
            sessionId,
            GetUtcNow(),
            reason);
    }

    public Task RevokeSessionsForMembership(
        Guid organizationMembershipId,
        string reason)
    {
        return _repository
            .RevokeSessionsForMembership(
                organizationMembershipId,
                GetUtcNow(),
                reason);
    }

    public Task RevokeSessionsForUser(
        Guid userId,
        string reason)
    {
        return _repository.RevokeSessionsForUser(
            userId,
            GetUtcNow(),
            reason);
    }

    private AuthenticationTokenPairDto
        CreateTokenPair(
            User user,
            OrganizationMembership membership,
            Guid sessionId,
            string refreshToken,
            DateTime refreshTokenExpiresAtUtc,
            DateTime now)
    {
        return new AuthenticationTokenPairDto
        {
            AuthenticationSessionId = sessionId,
            AccessToken =
                _jwtService.GenerateToken(
                    user,
                    membership,
                    sessionId),
            RefreshToken = refreshToken,
            AccessTokenExpiresAtUtc =
                now.AddMinutes(
                    _jwtOptions.ExpiryMinutes),
            RefreshTokenExpiresAtUtc =
                refreshTokenExpiresAtUtc
        };
    }

    private Task RecordRefreshTokenReuse(
        AuthenticationSession session,
        string reasonCode)
    {
        return _securityEventService.Record(
            new RecordAuthenticationSecurityEventDto
            {
                OrganizationId =
                    session.OrganizationId,
                UserId = session.UserId,
                AuthenticationSessionId =
                    session.Id,
                EventType =
                    AuthenticationSecurityEventTypes
                        .RefreshTokenReuse,
                Outcome =
                    AuthenticationSecurityOutcomes
                        .Blocked,
                ReasonCode = reasonCode
            });
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

    private DateTime GetUtcNow()
    {
        return _timeProvider
            .GetUtcNow()
            .UtcDateTime;
    }

    private static string GenerateToken()
    {
        var bytes =
            RandomNumberGenerator.GetBytes(
                TokenByteCount);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(
        string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return string.Empty;
        }

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    rawToken)));
    }

    private static UnauthorizedAccessException
        InvalidRefreshToken()
    {
        return new UnauthorizedAccessException(
            "The refresh token is invalid or has " +
            "expired.");
    }
}
