using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Moq;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Authentication;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Authentication;

public class AuthenticationSessionServiceTests
{
    private static readonly DateTimeOffset Now =
        new(
            2026,
            7,
            23,
            18,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task Create_StoresOnlyRefreshTokenHash()
    {
        var setup = CreateSetup();

        AuthenticationSession? savedSession = null;
        AuthenticationRefreshToken? savedToken = null;

        setup.Repository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<
                        AuthenticationSession>(),
                    It.IsAny<
                        AuthenticationRefreshToken>()))
            .Callback<
                AuthenticationSession,
                AuthenticationRefreshToken>(
                (session, token) =>
                {
                    savedSession = session;
                    savedToken = token;
                })
            .Returns(Task.CompletedTask);

        var tokens =
            await setup.Service.Create(
                setup.User,
                setup.Membership);

        Assert.NotNull(savedSession);
        Assert.NotNull(savedToken);
        Assert.NotEmpty(tokens.RefreshToken);
        Assert.Equal(
            HashToken(tokens.RefreshToken),
            savedToken.TokenHash);
        Assert.DoesNotContain(
            tokens.RefreshToken,
            savedToken.TokenHash,
            StringComparison.Ordinal);
        Assert.Equal(
            setup.Membership.Id,
            savedSession.OrganizationMembershipId);
        Assert.Equal(
            setup.User.SecurityStamp,
            savedSession.SecurityStamp);
        Assert.Equal(
            Now.AddDays(7).UtcDateTime,
            tokens.RefreshTokenExpiresAtUtc);

        setup.JwtService.Verify(
            service =>
                service.GenerateToken(
                    setup.User,
                    setup.Membership,
                    savedSession.Id),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_RotatesTokenAndIssuesNewPair()
    {
        var setup = CreateSetup();
        var rawToken = "current-refresh-token";

        var token =
            CreateRefreshToken(
                setup,
                rawToken);

        setup.Repository
            .Setup(repository =>
                repository.GetRefreshTokenByHash(
                    HashToken(rawToken)))
            .ReturnsAsync(token);

        AuthenticationRefreshToken?
            replacement = null;

        setup.Repository
            .Setup(repository =>
                repository.RotateRefreshToken(
                    token.Id,
                    It.IsAny<
                        AuthenticationRefreshToken>(),
                    Now.UtcDateTime))
            .Callback<
                Guid,
                AuthenticationRefreshToken,
                DateTime>(
                (_, next, _) =>
                    replacement = next)
            .ReturnsAsync(true);

        var result =
            await setup.Service.Refresh(rawToken);

        Assert.NotNull(replacement);
        Assert.NotEqual(
            rawToken,
            result.RefreshToken);
        Assert.Equal(
            HashToken(result.RefreshToken),
            replacement.TokenHash);
        Assert.Equal(
            "access-token",
            result.AccessToken);
        Assert.Equal(
            setup.Organization.Id,
            result.OrganizationId);
    }

    [Fact]
    public async Task Refresh_ReusedTokenRevokesSession()
    {
        var setup = CreateSetup();
        var rawToken = "replayed-refresh-token";

        var token =
            CreateRefreshToken(
                setup,
                rawToken);

        token.ConsumedAtUtc =
            Now.AddMinutes(-1).UtcDateTime;

        setup.Repository
            .Setup(repository =>
                repository.GetRefreshTokenByHash(
                    HashToken(rawToken)))
            .ReturnsAsync(token);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => setup.Service.Refresh(
                rawToken));

        setup.Repository.Verify(
            repository =>
                repository.RevokeSession(
                    token.AuthenticationSessionId,
                    Now.UtcDateTime,
                    It.Is<string>(reason =>
                        reason.Contains(
                            "reuse",
                            StringComparison
                                .OrdinalIgnoreCase))),
            Times.Once);

        setup.Repository.Verify(
            repository =>
                repository.RotateRefreshToken(
                    It.IsAny<Guid>(),
                    It.IsAny<
                        AuthenticationRefreshToken>(),
                    It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task Refresh_ConcurrentUseRevokesSession()
    {
        var setup = CreateSetup();
        var rawToken = "concurrent-refresh-token";

        var token =
            CreateRefreshToken(
                setup,
                rawToken);

        setup.Repository
            .Setup(repository =>
                repository.GetRefreshTokenByHash(
                    HashToken(rawToken)))
            .ReturnsAsync(token);

        setup.Repository
            .Setup(repository =>
                repository.RotateRefreshToken(
                    token.Id,
                    It.IsAny<
                        AuthenticationRefreshToken>(),
                    Now.UtcDateTime))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => setup.Service.Refresh(
                rawToken));

        setup.Repository.Verify(
            repository =>
                repository.RevokeSession(
                    token.AuthenticationSessionId,
                    Now.UtcDateTime,
                    It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_DisabledMembershipIsRejected()
    {
        var setup = CreateSetup();
        var rawToken = "disabled-membership-token";

        var token =
            CreateRefreshToken(
                setup,
                rawToken);

        setup.Membership.IsActive = false;

        setup.Repository
            .Setup(repository =>
                repository.GetRefreshTokenByHash(
                    HashToken(rawToken)))
            .ReturnsAsync(token);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => setup.Service.Refresh(
                rawToken));

        setup.Repository.Verify(
            repository =>
                repository.RevokeSession(
                    token.AuthenticationSessionId,
                    Now.UtcDateTime,
                    It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task
        Refresh_ChangedSecurityStampRevokesSession()
    {
        var setup = CreateSetup();
        var rawToken = "old-credential-token";

        var token =
            CreateRefreshToken(
                setup,
                rawToken);

        token.AuthenticationSession.SecurityStamp =
            Guid.NewGuid();

        setup.Repository
            .Setup(repository =>
                repository.GetRefreshTokenByHash(
                    HashToken(rawToken)))
            .ReturnsAsync(token);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => setup.Service.Refresh(
                rawToken));

        setup.Repository.Verify(
            repository =>
                repository.RevokeSession(
                    token.AuthenticationSessionId,
                    Now.UtcDateTime,
                    It.IsAny<string>()),
            Times.Once);

        setup.Repository.Verify(
            repository =>
                repository.RotateRefreshToken(
                    It.IsAny<Guid>(),
                    It.IsAny<
                        AuthenticationRefreshToken>(),
                    It.IsAny<DateTime>()),
            Times.Never);
    }

    private static ServiceSetup CreateSetup()
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = Roles.TreasuryOfficer
        };

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = "MOBALLS",
            Name = "Moballs Limited",
            Slug = "moballs"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Ada",
            LastName = "Okafor",
            Email = "ada@example.com",
            PasswordHash = "not-used",
            EmailVerifiedAtUtc =
                Now.AddDays(-10).UtcDateTime,
            IsActive = true,
            RoleId = role.Id,
            Role = role
        };

        var membership =
            new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    organization.Id,
                Organization = organization,
                UserId = user.Id,
                User = user,
                RoleId = role.Id,
                Role = role,
                IsActive = true,
                IsDefault = true
            };

        user.OrganizationMemberships.Add(
            membership);

        var repository =
            new Mock<
                IAuthenticationSessionRepository>();

        repository
            .Setup(item => item.SaveChanges())
            .Returns(Task.CompletedTask);

        var jwtService =
            new Mock<IJwtService>();

        jwtService
            .Setup(service =>
                service.GenerateToken(
                    user,
                    membership,
                    It.IsAny<Guid>()))
            .Returns("access-token");

        var service =
            new AuthenticationSessionService(
                repository.Object,
                jwtService.Object,
                Options.Create(
                    new JwtSettingsOptions
                    {
                        SecretKey =
                            "test-secret-key-that-is-" +
                            "long-enough-for-hmac-sha256",
                        Issuer = "Treasury.Tests",
                        Audience = "Treasury.Tests",
                        ExpiryMinutes = 15
                    }),
                Options.Create(
                    new AuthenticationSessionOptions
                    {
                        RefreshTokenDays = 7
                    }),
                new FixedTimeProvider(Now));

        return new ServiceSetup(
            service,
            repository,
            jwtService,
            organization,
            user,
            membership);
    }

    private static AuthenticationRefreshToken
        CreateRefreshToken(
            ServiceSetup setup,
            string rawToken)
    {
        var session =
            new AuthenticationSession
            {
                Id = Guid.NewGuid(),
                UserId = setup.User.Id,
                User = setup.User,
                OrganizationId =
                    setup.Organization.Id,
                Organization =
                    setup.Organization,
                OrganizationMembershipId =
                    setup.Membership.Id,
                OrganizationMembership =
                    setup.Membership,
                CreatedAtUtc =
                    Now.AddHours(-1).UtcDateTime,
                LastActivityAtUtc =
                    Now.AddHours(-1).UtcDateTime,
                ExpiresAtUtc =
                    Now.AddDays(6).UtcDateTime,
                SecurityStamp =
                    setup.User.SecurityStamp
            };

        return new AuthenticationRefreshToken
        {
            Id = Guid.NewGuid(),
            AuthenticationSessionId =
                session.Id,
            AuthenticationSession = session,
            TokenHash = HashToken(rawToken),
            CreatedAtUtc =
                Now.AddHours(-1).UtcDateTime,
            ExpiresAtUtc =
                session.ExpiresAtUtc
        };
    }

    private static string HashToken(
        string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token)));
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(
            DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset
            GetUtcNow()
        {
            return _now;
        }
    }

    private sealed record ServiceSetup(
        AuthenticationSessionService Service,
        Mock<IAuthenticationSessionRepository>
            Repository,
        Mock<IJwtService> JwtService,
        Organization Organization,
        User User,
        OrganizationMembership Membership);
}
