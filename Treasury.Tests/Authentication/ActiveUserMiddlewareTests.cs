using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using Treasury.Api.Middleware;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Authentication;

public class ActiveUserMiddlewareTests
{
    [Fact]
    public async Task RevokedSession_IsRejected()
    {
        var setup = CreateSetup();

        setup.SessionService
            .Setup(service =>
                service.IsSessionActive(
                    setup.SessionId,
                    setup.User.Id,
                    setup.Membership.Id))
            .ReturnsAsync(false);

        await setup.Middleware.InvokeAsync(
            setup.Context,
            setup.UserRepository.Object,
            setup.SessionService.Object);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            setup.Context.Response.StatusCode);
        Assert.False(setup.NextWasCalled());
    }

    [Fact]
    public async Task MembershipRole_IsUsedInsteadOfLegacyGlobalRole()
    {
        var setup = CreateSetup(
            globalRoleName: Roles.Admin,
            membershipRoleName:
                Roles.TreasuryOfficer);

        setup.SessionService
            .Setup(service =>
                service.IsSessionActive(
                    setup.SessionId,
                    setup.User.Id,
                    setup.Membership.Id))
            .ReturnsAsync(true);

        await setup.Middleware.InvokeAsync(
            setup.Context,
            setup.UserRepository.Object,
            setup.SessionService.Object);

        Assert.Equal(
            StatusCodes.Status200OK,
            setup.Context.Response.StatusCode);
        Assert.True(setup.NextWasCalled());
    }

    [Fact]
    public async Task DisabledMembership_IsRejected()
    {
        var setup = CreateSetup();

        setup.Membership.IsActive = false;

        setup.SessionService
            .Setup(service =>
                service.IsSessionActive(
                    setup.SessionId,
                    setup.User.Id,
                    setup.Membership.Id))
            .ReturnsAsync(true);

        await setup.Middleware.InvokeAsync(
            setup.Context,
            setup.UserRepository.Object,
            setup.SessionService.Object);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            setup.Context.Response.StatusCode);
        Assert.False(setup.NextWasCalled());
    }

    private static MiddlewareSetup CreateSetup(
        string globalRoleName =
            Roles.FinanceManager,
        string membershipRoleName =
            Roles.TreasuryOfficer)
    {
        var globalRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = globalRoleName
        };

        var membershipRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = membershipRoleName
        };

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = "MOBALLS",
            Name = "Moballs Limited",
            Slug = "moballs",
            IsActive = true
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            PasswordHash = "not-used",
            EmailVerifiedAtUtc = DateTime.UtcNow,
            IsActive = true,
            RoleId = globalRole.Id,
            Role = globalRole
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
                RoleId = membershipRole.Id,
                Role = membershipRole,
                IsActive = true,
                IsDefault = true
            };

        user.OrganizationMemberships.Add(
            membership);

        var sessionId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),
            new Claim(
                ClaimTypes.Role,
                membershipRole.Name),
            new Claim(
                CustomClaimTypes.OrganizationId,
                organization.Id.ToString()),
            new Claim(
                CustomClaimTypes
                    .OrganizationMembershipId,
                membership.Id.ToString()),
            new Claim(
                CustomClaimTypes
                    .AuthenticationSessionId,
                sessionId.ToString())
        };

        var context =
            new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        claims,
                        "test"))
            };

        context.Response.Body =
            new MemoryStream();

        var userRepository =
            new Mock<IUserRepository>();

        userRepository
            .Setup(repository =>
                repository.GetById(user.Id))
            .ReturnsAsync(user);

        var sessionService =
            new Mock<
                IAuthenticationSessionService>();

        var nextCalled = false;

        var middleware =
            new ActiveUserMiddleware(
                _ =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                });

        return new MiddlewareSetup(
            middleware,
            context,
            userRepository,
            sessionService,
            user,
            membership,
            sessionId,
            () => nextCalled);
    }

    private sealed record MiddlewareSetup(
        ActiveUserMiddleware Middleware,
        DefaultHttpContext Context,
        Mock<IUserRepository> UserRepository,
        Mock<IAuthenticationSessionService>
            SessionService,
        User User,
        OrganizationMembership Membership,
        Guid SessionId,
        Func<bool> NextWasCalled);
}
