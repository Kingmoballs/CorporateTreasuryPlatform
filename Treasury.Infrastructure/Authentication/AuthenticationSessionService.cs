using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

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

    public AuthenticationSessionService(
        IAuthenticationSessionRepository
            repository,
        IJwtService jwtService,
        IOptions<JwtSettingsOptions> jwtOptions,
        IOptions<AuthenticationSessionOptions>
            sessionOptions,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _jwtService = jwtService;
        _jwtOptions = jwtOptions.Value;
        _sessionOptions = sessionOptions.Value;
        _timeProvider = timeProvider;
    }

    public async Task<AuthenticationTokenPairDto>
        Create(
            User user,
            OrganizationMembership membership)
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
                User = user,
                OrganizationId =
                    membership.OrganizationId,
                Organization =
                    membership.Organization,
                OrganizationMembershipId =
                    membership.Id,
                OrganizationMembership =
                    membership,
                CreatedAtUtc = now,
                LastActivityAtUtc = now,
                ExpiresAtUtc = sessionExpiry,
                SecurityStamp = user.SecurityStamp
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

        return CreateTokenPair(
            user,
            membership,
            session.Id,
            rawRefreshToken,
            sessionExpiry,
            now);
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

            throw InvalidRefreshToken();
        }

        var tokens = CreateTokenPair(
            user,
            membership,
            session.Id,
            replacementRawToken,
            session.ExpiresAtUtc,
            now);

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
