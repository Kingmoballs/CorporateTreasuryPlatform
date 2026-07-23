using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Authentication;

public class OrganizationAccessServiceTests
{
    [Fact]
    public async Task
        GetAvailableOrganizations_MarksCurrentMembership()
    {
        var setup = CreateSetup();
        var current =
            CreateMembership(
                setup.UserId,
                setup.CurrentMembershipId,
                "CURRENT");

        var other =
            CreateMembership(
                setup.UserId,
                Guid.NewGuid(),
                "OTHER");

        setup.Repository
            .Setup(item =>
                item.GetActiveMembershipsForUser(
                    setup.UserId))
            .ReturnsAsync(
                new[]
                {
                    current,
                    other
                });

        var result =
            await setup.Service
                .GetAvailableOrganizations();

        Assert.Equal(2, result.Count);
        Assert.True(
            result.Single(item =>
                item.OrganizationMembershipId ==
                    setup.CurrentMembershipId)
                .IsCurrent);
        Assert.False(
            result.Single(item =>
                item.OrganizationMembershipId ==
                    other.Id)
                .IsCurrent);
    }

    [Fact]
    public async Task
        SwitchOrganization_UsesOnlyOwnedMembership()
    {
        var setup = CreateSetup();
        var target =
            CreateMembership(
                setup.UserId,
                Guid.NewGuid(),
                "TARGET");

        var replacementSessionId =
            Guid.NewGuid();

        setup.Repository
            .Setup(item =>
                item.GetActiveMembershipForUser(
                    target.Id,
                    setup.UserId))
            .ReturnsAsync(target);

        setup.SessionService
            .Setup(item =>
                item.SwitchOrganization(
                    target.User,
                    target,
                    setup.CurrentSessionId))
            .ReturnsAsync(
                new AuthenticationTokenPairDto
                {
                    AuthenticationSessionId =
                        replacementSessionId,
                    AccessToken = "access",
                    RefreshToken = "refresh"
                });

        var result =
            await setup.Service.SwitchOrganization(
                new SwitchOrganizationDto
                {
                    OrganizationMembershipId =
                        target.Id
                });

        Assert.Equal(
            target.OrganizationId,
            result.OrganizationId);
        Assert.Equal(
            target.Id,
            result.OrganizationMembershipId);

        setup.Repository.Verify(
            item => item.GetActiveMembershipForUser(
                target.Id,
                setup.UserId),
            Times.Once);

        setup.SecurityEvents.Verify(
            item => item.Record(
                It.Is<
                    RecordAuthenticationSecurityEventDto>(
                    dto =>
                        dto.EventType ==
                            AuthenticationSecurityEventTypes
                                .OrganizationSwitched &&
                        dto.AuthenticationSessionId ==
                            replacementSessionId)),
            Times.Once);
    }

    [Fact]
    public async Task
        SwitchOrganization_UnknownMembershipIsHidden()
    {
        var setup = CreateSetup();
        var membershipId = Guid.NewGuid();

        setup.Repository
            .Setup(item =>
                item.GetActiveMembershipForUser(
                    membershipId,
                    setup.UserId))
            .ReturnsAsync(
                (OrganizationMembership?)null);

        await Assert.ThrowsAsync<
            ResourceNotFoundException>(
            () => setup.Service.SwitchOrganization(
                new SwitchOrganizationDto
                {
                    OrganizationMembershipId =
                        membershipId
                }));

        setup.SessionService.Verify(
            item => item.SwitchOrganization(
                It.IsAny<User>(),
                It.IsAny<
                    OrganizationMembership>(),
                It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task
        SwitchOrganization_CurrentMembershipIsRejected()
    {
        var setup = CreateSetup();

        await Assert.ThrowsAsync<
            BusinessRuleException>(
            () => setup.Service.SwitchOrganization(
                new SwitchOrganizationDto
                {
                    OrganizationMembershipId =
                        setup.CurrentMembershipId
                }));

        setup.Repository.Verify(
            item => item.GetActiveMembershipForUser(
                It.IsAny<Guid>(),
                It.IsAny<Guid>()),
            Times.Never);
    }

    private static ServiceSetup CreateSetup()
    {
        var userId = Guid.NewGuid();
        var currentOrganizationId =
            Guid.NewGuid();
        var currentMembershipId =
            Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();

        var repository =
            new Mock<
                IOrganizationAccessRepository>();

        var sessionService =
            new Mock<
                IAuthenticationSessionService>();

        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser.SetupGet(item => item.UserId)
            .Returns(userId);

        currentUser
            .SetupGet(item => item.OrganizationId)
            .Returns(currentOrganizationId);

        currentUser
            .SetupGet(item =>
                item.OrganizationMembershipId)
            .Returns(currentMembershipId);

        currentUser
            .SetupGet(item =>
                item.AuthenticationSessionId)
            .Returns(currentSessionId);

        var securityEvents =
            new Mock<
                IAuthenticationSecurityEventService>();

        securityEvents
            .Setup(item => item.Record(
                It.IsAny<
                    RecordAuthenticationSecurityEventDto>()))
            .Returns(Task.CompletedTask);

        var service =
            new OrganizationAccessService(
                repository.Object,
                sessionService.Object,
                currentUser.Object,
                securityEvents.Object);

        return new ServiceSetup(
            service,
            repository,
            sessionService,
            securityEvents,
            userId,
            currentMembershipId,
            currentSessionId);
    }

    private static OrganizationMembership
        CreateMembership(
            Guid userId,
            Guid membershipId,
            string organizationCode)
    {
        var organization =
            new Organization
            {
                Id = Guid.NewGuid(),
                Code = organizationCode,
                Name =
                    organizationCode +
                    " Organization"
            };

        var role =
            new Role
            {
                Id = Guid.NewGuid(),
                Name = Roles.TreasuryOfficer
            };

        var user =
            new User
            {
                Id = userId,
                Email = "user@example.com",
                RoleId = role.Id,
                Role = role,
                EmailVerifiedAtUtc =
                    DateTime.UtcNow
            };

        return new OrganizationMembership
        {
            Id = membershipId,
            UserId = userId,
            User = user,
            OrganizationId = organization.Id,
            Organization = organization,
            RoleId = role.Id,
            Role = role,
            IsActive = true
        };
    }

    private sealed record ServiceSetup(
        OrganizationAccessService Service,
        Mock<IOrganizationAccessRepository>
            Repository,
        Mock<IAuthenticationSessionService>
            SessionService,
        Mock<IAuthenticationSecurityEventService>
            SecurityEvents,
        Guid UserId,
        Guid CurrentMembershipId,
        Guid CurrentSessionId);
}
