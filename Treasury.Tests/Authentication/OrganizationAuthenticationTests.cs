using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Moq;
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
    public async Task Register_AddsDefaultOrganizationMembership()
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = Roles.TreasuryOfficer
        };

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = OrganizationDefaults
                .OrganizationCode,
            Name = OrganizationDefaults
                .OrganizationName,
            Slug = OrganizationDefaults
                .OrganizationSlug
        };

        var userRepository =
            new Mock<IUserRepository>();

        userRepository
            .Setup(repository =>
                repository.GetByEmail(
                    It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        User? savedUser = null;

        userRepository
            .Setup(repository =>
                repository.Add(
                    It.IsAny<User>()))
            .Callback<User>(user =>
                savedUser = user)
            .Returns(Task.CompletedTask);

        userRepository
            .Setup(repository =>
                repository.SaveChanges())
            .Returns(Task.CompletedTask);

        var roleRepository =
            new Mock<IRoleRepository>();

        roleRepository
            .Setup(repository =>
                repository.GetByName(
                    Roles.TreasuryOfficer))
            .ReturnsAsync(role);

        var organizationRepository =
            new Mock<IOrganizationRepository>();

        organizationRepository
            .Setup(repository =>
                repository.GetByCode(
                    OrganizationDefaults
                        .OrganizationCode))
            .ReturnsAsync(organization);

        var jwtService =
            new Mock<IJwtService>();

        jwtService
            .Setup(service =>
                service.GenerateToken(
                    It.IsAny<User>()))
            .Returns("test-token");

        var service = new AuthService(
            userRepository.Object,
            jwtService.Object,
            roleRepository.Object,
            organizationRepository.Object);

        var response = await service.Register(
            new RegisterDto
            {
                FirstName = "Ada",
                LastName = "Okafor",
                Email = "ada@example.com",
                Password = "SecurePassword123!"
            });

        Assert.NotNull(savedUser);

        var membership = Assert.Single(
            savedUser.OrganizationMemberships);

        Assert.Equal(
            organization.Id,
            membership.OrganizationId);

        Assert.Equal(role.Id, membership.RoleId);
        Assert.True(membership.IsActive);
        Assert.True(membership.IsDefault);
        Assert.Equal(
            organization.Id,
            response.OrganizationId);
        Assert.Equal(
            organization.Code,
            response.OrganizationCode);
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

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<
                        string,
                        string?>
                    {
                        [
                            "JwtSettings:SecretKey"
                        ] =
                            "a-test-secret-key-that-is-" +
                            "long-enough-for-hmac-sha256",
                        [
                            "JwtSettings:Issuer"
                        ] = "Treasury.Tests",
                        [
                            "JwtSettings:Audience"
                        ] = "Treasury.Tests",
                        [
                            "JwtSettings:ExpiryMinutes"
                        ] = "30"
                    })
                .Build();

        var service =
            new JwtService(configuration);

        var encodedToken =
            service.GenerateToken(user);

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
    }
}
