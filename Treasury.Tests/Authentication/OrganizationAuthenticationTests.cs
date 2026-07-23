using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Moq;
using Treasury.Api.Controllers;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Application.Services;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Authentication;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Authentication;

public class OrganizationAuthenticationTests
{
    [Fact]
    public void AuthController_DoesNotExposePublicRegistration()
    {
        var publicRegisterMethod =
            typeof(AuthController)
                .GetMethods()
                .FirstOrDefault(method =>
                    string.Equals(
                        method.Name,
                        "Register",
                        StringComparison.Ordinal));

        Assert.Null(publicRegisterMethod);
    }

    [Fact]
    public async Task Login_RejectsUnverifiedEmail()
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = Roles.TreasuryOfficer
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Ada",
            LastName = "Okafor",
            Email = "ada@example.com",
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    "SecurePassword123!"),
            EmailVerifiedAtUtc = null,
            IsActive = true,
            RoleId = role.Id,
            Role = role
        };

        var userRepository =
            new Mock<IUserRepository>();

        userRepository
            .Setup(repository =>
                repository.GetByEmail(
                    user.Email))
            .ReturnsAsync(user);

        var sessionService =
            new Mock<
                IAuthenticationSessionService>();

        var currentUserService =
            new Mock<ICurrentUserService>();

        var service = new AuthService(
            userRepository.Object,
            Mock.Of<ILoginAttemptService>(),
            Mock.Of<
                IMultiFactorAuthenticationService>(),
            sessionService.Object,
            currentUserService.Object,
            Mock.Of<
                IAuthenticationSecurityEventService>());

        var exception =
            await Assert.ThrowsAsync<
                UnauthorizedAccessException>(
                () => service.Login(
                    new LoginDto
                    {
                        Email = user.Email,
                        Password =
                            "SecurePassword123!"
                    }));

        Assert.Contains(
            "Invalid credentials",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        sessionService.Verify(
            item =>
                item.Create(
                    It.IsAny<User>(),
                    It.IsAny<
                        OrganizationMembership>()),
            Times.Never);
    }

    [Fact]
    public async Task Login_CreatesServerSession()
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
            Email = "ada@example.com",
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    "SecurePassword123!"),
            EmailVerifiedAtUtc = DateTime.UtcNow,
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

        var userRepository =
            new Mock<IUserRepository>();

        userRepository
            .Setup(repository =>
                repository.GetByEmail(user.Email))
            .ReturnsAsync(user);

        var sessionService =
            new Mock<
                IAuthenticationSessionService>();

        var loginAttemptService =
            new Mock<ILoginAttemptService>();

        loginAttemptService
            .Setup(service =>
                service
                    .CompleteSuccessfulAttempt(
                        user.Id))
            .ReturnsAsync(true);

        sessionService
            .Setup(service =>
                service.Create(user, membership))
            .ReturnsAsync(
                new AuthenticationTokenPairDto
                {
                    AccessToken = "access",
                    RefreshToken = "refresh",
                    AccessTokenExpiresAtUtc =
                        DateTime.UtcNow
                            .AddMinutes(15),
                    RefreshTokenExpiresAtUtc =
                        DateTime.UtcNow.AddDays(7)
                });

        var service = new AuthService(
            userRepository.Object,
            loginAttemptService.Object,
            Mock.Of<
                IMultiFactorAuthenticationService>(),
            sessionService.Object,
            Mock.Of<ICurrentUserService>(),
            Mock.Of<
                IAuthenticationSecurityEventService>());

        var response =
            await service.Login(
                new LoginDto
                {
                    Email = user.Email,
                    Password =
                        "SecurePassword123!"
                });

        Assert.Equal(
            "access",
            response.AccessToken);
        Assert.Equal(
            "refresh",
            response.RefreshToken);
        Assert.Equal(
            organization.Id,
            response.OrganizationId);
    }

    [Fact]
    public async Task LogoutAll_RevokesEveryUserSession()
    {
        var userId = Guid.NewGuid();

        var sessionService =
            new Mock<
                IAuthenticationSessionService>();

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .SetupGet(service => service.UserId)
            .Returns(userId);

        var service = new AuthService(
            Mock.Of<IUserRepository>(),
            Mock.Of<ILoginAttemptService>(),
            Mock.Of<
                IMultiFactorAuthenticationService>(),
            sessionService.Object,
            currentUserService.Object,
            Mock.Of<
                IAuthenticationSecurityEventService>());

        await service.LogoutAll();

        sessionService.Verify(
            item =>
                item.RevokeSessionsForUser(
                    userId,
                    It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void GenerateToken_IncludesOrganizationClaims()
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = Roles.FinanceManager
        };

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = "MOBALLS",
            Name = "Moballs Limited",
            Slug = "moballs-limited"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Ada",
            LastName = "Okafor",
            Email = "ada@example.com",
            PasswordHash = "not-used",
            EmailVerifiedAtUtc = DateTime.UtcNow,
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

        var sessionId = Guid.NewGuid();

        var service =
            new JwtService(
                Options.Create(
                    new JwtSettingsOptions
                    {
                        SecretKey =
                            "a-test-secret-key-that-is-" +
                            "long-enough-for-hmac-sha256",
                        Issuer = "Treasury.Tests",
                        Audience = "Treasury.Tests",
                        ExpiryMinutes = 30
                    }),
                TimeProvider.System);

        var encodedToken =
            service.GenerateToken(
                user,
                membership,
                sessionId);

        var token =
            new JwtSecurityTokenHandler()
                .ReadJwtToken(encodedToken);

        Assert.Equal(
            organization.Id.ToString(),
            token.Claims.Single(claim =>
                claim.Type ==
                CustomClaimTypes.OrganizationId)
                .Value);

        Assert.Equal(
            organization.Code,
            token.Claims.Single(claim =>
                claim.Type ==
                CustomClaimTypes.OrganizationCode)
                .Value);

        Assert.Equal(
            membership.Id.ToString(),
            token.Claims.Single(claim =>
                claim.Type ==
                CustomClaimTypes
                    .OrganizationMembershipId)
                .Value);

        Assert.Equal(
            sessionId.ToString(),
            token.Claims.Single(claim =>
                claim.Type ==
                CustomClaimTypes
                    .AuthenticationSessionId)
                .Value);
    }
}
